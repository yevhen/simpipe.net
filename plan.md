# Parallel Enrichment Implementation Plan

## Architecture Overview

The parallel enrichment feature will be implemented using a `ParallelBlock<T>` that implements `IActionBlock<T>`. This block will coordinate multiple child blocks (enrichments) that execute in parallel. Since enrichments are independent and don't modify shared fields, we can safely execute them concurrently without synchronization concerns.

### Key Components

1. **ParallelBlock<T>** - Coordinates parallel enrichment execution
2. **ParallelBuilder<T>** - Entry point for fluent API  
3. **ParallelPipeBuilder<T>** - Integrates with Pipe API

## Implementation Increments

### Increment 1: Walking Skeleton - Single Action Enrichment
**Outcome**: Basic ParallelBlock that executes one enrichment and applies result

**Test First**:
```csharp
[Test]
public async Task Should_execute_single_action_enrichment()
{
    var item = new TestItem { Id = 1, Value = "test" };
    var enriched = false;
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => {
                await Task.Delay(1);
                item.EnrichedValue = item.Id * 2;
                enriched = true;
            }))
        .Id("enricher")
        .ToPipe();
    
    await pipe.Send(item);
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(enriched, Is.True);
    Assert.That(item.EnrichedValue, Is.EqualTo(2));
}
```

**Implementation**:
- Create `ParallelBlock<T>` with minimal Send/Complete
- Create `ParallelBuilder<T>` with Action method accepting Func<T, Task>
- Create child ActionBlock for each enrichment
- Execute all enrichments concurrently using Task.WhenAll

### Increment 2: Multiple Parallel Enrichments
**Outcome**: Execute multiple enrichments in parallel, apply serially

**Test First**:
```csharp
[Test] 
public async Task Should_execute_multiple_enrichments_in_parallel()
{
    var item = new TestItem { Id = 1, Text = "hello" };
    var executionOrder = new ConcurrentBag<string>();
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => { 
                executionOrder.Add("id");
                await Task.Delay(50);
                item.EnrichedId = item.Id * 2;
            })
            .Action(async item => {
                executionOrder.Add("text");
                await Task.Delay(10);
                item.TextLength = item.Text.Length;
            }))
        .Id("enricher")
        .ToPipe();
    
    await pipe.Send(item);
    pipe.Complete();
    await pipe.Completion;
    
    // Verify parallel execution (both started before either finished)
    Assert.That(executionOrder.Count, Is.EqualTo(2));
    Assert.That(item.EnrichedId, Is.EqualTo(2));
    Assert.That(item.TextLength, Is.EqualTo(5));
}
```

**Implementation**:
- Store multiple enrichment actions in ParallelBuilder
- Execute all enrichments concurrently using Task.WhenAll

### Increment 3: Conditional Enrichment (When clause)
**Outcome**: Skip enrichments based on item condition

**Test First**:
```csharp
[Test]
public async Task Should_skip_enrichment_when_condition_false()
{
    var items = new[] {
        new TestItem { Id = 1, Text = "hello" },
        new TestItem { Id = 2, Text = null }
    };
    
    var executedFor = new List<int>();
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => { 
                executedFor.Add(item.Id);
                item.TextLength = item.Text.Length;
            })
            .Filter(item => !string.IsNullOrEmpty(item.Text)))
        .Id("enricher")
        .ToPipe();
    
    foreach (var item in items)
        await pipe.Send(item);
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(executedFor, Is.EqualTo(new[] { 1 }));
    Assert.That(items[0].TextLength, Is.EqualTo(5));
    Assert.That(items[1].TextLength, Is.EqualTo(0));
}
```

**Implementation**:
- Add Filter method to enrichment configuration
- Check condition before executing action
- Skip enrichment if condition is false

### Increment 4: Error Handling
**Outcome**: Handle enrichment failures gracefully

**Test First**:
```csharp
[Test]
public async Task Should_fault_block_on_enrichment_failure()
{
    var item = new TestItem { Id = 1, Text = "test" };
    var started = new ConcurrentBag<string>();
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => {
                started.Add("enrichment1");
                await Task.Delay(10);
                throw new InvalidOperationException("enrichment1 failed");
            })
            .Action(async item => {
                started.Add("enrichment2");
                await Task.Delay(50);
                item.TextLength = item.Text.Length;
            }))
        .Id("enricher")
        .ToPipe();
    
    await pipe.Send(item);
    pipe.Complete();
    
    // Should throw the exception from failed enrichment
    var ex = Assert.ThrowsAsync<InvalidOperationException>(
        async () => await pipe.Completion);
    Assert.That(ex.Message, Is.EqualTo("enrichment1 failed"));
    
    // Both enrichments should have started (parallel execution)
    Assert.That(started.Count, Is.EqualTo(2));
}
```

**Implementation**:
- Let enrichments execute in parallel
- If any enrichment fails, fault the block
- Store first exception and propagate through Completion

### Increment 5: Basic Batch Enrichment
**Outcome**: Support batch enrichments alongside action enrichments

**Test First**:
```csharp
[Test]
public async Task Should_execute_batch_enrichment()
{
    var items = new[] {
        new TestItem { Id = 1, Text = "hello" },
        new TestItem { Id = 2, Text = "world" },
        new TestItem { Id = 3, Text = "test" }
    };
    
    var batchesExecuted = 0;
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Batch(2, async items => {
                await Task.Delay(10);
                batchesExecuted++;
                foreach (var item in items)
                    item.BatchLength = item.Text.Length;
            }))
        .Id("enricher")
        .ToPipe();
    
    // Send 3 items - should trigger 2 batches
    foreach (var item in items)
        await pipe.Send(item);
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(batchesExecuted, Is.EqualTo(2));
    Assert.That(items[0].BatchLength, Is.EqualTo(5));
    Assert.That(items[1].BatchLength, Is.EqualTo(5));
    Assert.That(items[2].BatchLength, Is.EqualTo(4));
}
```

**Implementation**:
- Add Batch method accepting batch size and Func<T[], Task>
- Use internal BatchBlock to accumulate items
- Execute batch action when size reached

### Increment 6: Batch Timeout Support
**Outcome**: Flush incomplete batches after timeout

**Test First**:
```csharp
[Test]
public async Task Should_flush_batch_on_timeout()
{
    var item = new TestItem { Id = 1 };
    var batchExecuted = false;
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Batch(10, async items => {
                batchExecuted = true;
                foreach (var item in items)
                    item.BatchResult = item.Id * 10;
            })
            .BatchTriggerPeriod(TimeSpan.FromMilliseconds(50)))
        .Id("enricher")
        .ToPipe();
    
    await pipe.Send(item);
    
    // Wait for timeout
    await Task.Delay(100);
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(batchExecuted, Is.True);
    Assert.That(item.BatchResult, Is.EqualTo(10));
}
```

**Implementation**:
- Add BatchTriggerPeriod configuration
- Use TimerBatchBlock instead of BatchBlock
- Ensure proper cleanup on completion

### Increment 7: Per-Enrichment Parallelism Control
**Outcome**: Control concurrency per enrichment

**Test First**:
```csharp
[Test]
public async Task Should_respect_per_enrichment_parallelism()
{
    var maxConcurrency = 0;
    var currentConcurrency = 0;
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => {
                Interlocked.Increment(ref currentConcurrency);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
                await Task.Delay(50);
                Interlocked.Decrement(ref currentConcurrency);
                item.Result = item.Id;
            })
            .DegreeOfParallelism(2))
        .Id("enricher")
        .ToPipe();
    
    // Send 5 items
    for (var i = 1; i <= 5; i++)
        await pipe.Send(new TestItem { Id = i });
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(maxConcurrency, Is.LessThanOrEqualTo(2));
    Assert.That(maxConcurrency, Is.GreaterThanOrEqualTo(1));
}
```

**Implementation**:
- Add DegreeOfParallelism configuration
- Pass parallelism parameter to underlying ActionBlock
- Default to 1 if not specified

### Increment 8: Counter Support
**Outcome**: Expose counters for monitoring

**Test First**:
```csharp
[Test]
public async Task Should_expose_enrichment_counters()
{
    var processedCount = 0;
    var executeStarted = new TaskCompletionSource();
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => { 
                executeStarted.TrySetResult();
                await Task.Delay(100); // Keep it running
                item.Result = item.Id * 2;
                Interlocked.Increment(ref processedCount);
            })
            .Id("multiply"))
        .Id("enricher")
        .ToPipe();
    
    // Send items but don't wait for completion
    var sendTask1 = pipe.Send(new TestItem { Id = 1 });
    var sendTask2 = pipe.Send(new TestItem { Id = 2 });
    
    // Wait for execution to start
    await executeStarted.Task;
    await Task.Delay(10); // Let counters update
    
    var parallelBlock = (ParallelBlock<TestItem>)pipe.Block;
    var counter = parallelBlock.GetEnrichmentCounter("multiply");
    Assert.That(counter.InputCount, Is.EqualTo(0)); // Items moved to working
    Assert.That(counter.WorkingCount, Is.GreaterThan(0)); // Items being processed
    
    // Complete and verify final state
    await Task.WhenAll(sendTask1, sendTask2);
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(processedCount, Is.EqualTo(2));
    Assert.That(counter.WorkingCount, Is.EqualTo(0)); // All completed
}
```

**Implementation**:
- Store enrichment names via Id method
- Expose GetEnrichmentCounter method
- Return IItemCounter from child blocks

### Increment 9: Cancellation Token Support
**Outcome**: Support per-enrichment cancellation tokens

**Test First**:
```csharp
[Test]
public async Task Should_support_per_enrichment_cancellation()
{
    var cts1 = new CancellationTokenSource();
    var cts2 = new CancellationTokenSource();
    var executed1 = false;
    var executed2 = false;
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action(async item => {
                await Task.Delay(100, cts1.Token);
                executed1 = true;
                item.Result1 = "done1";
            })
            .CancellationToken(cts1.Token)
            .Action(async item => {
                await Task.Delay(50, cts2.Token);
                executed2 = true;
                item.Result2 = "done2";
            })
            .CancellationToken(cts2.Token))
        .Id("enricher")
        .ToPipe();
    
    // Cancel first enrichment
    cts1.Cancel();
    
    var item = new TestItem();
    await pipe.Send(item);
    pipe.Complete();
    await pipe.Completion;
    
    // First enrichment cancelled, second should complete
    Assert.That(executed1, Is.False);
    Assert.That(executed2, Is.True);
    Assert.That(item.Result1, Is.Null);
    Assert.That(item.Result2, Is.EqualTo("done2"));
}
```

**Implementation**:
- Add CancellationToken configuration
- Pass cancellation tokens to respective child blocks
- Each enrichment handles its own cancellation independently

## Testing Strategy

### Unit Tests
- Each increment has focused unit tests through Pipe.Parallel API
- Test both success and error paths
- Use minimal test items/mocks
- Cast to ParallelBlock only when accessing advanced APIs (counters)

### Integration Tests
- All tests are effectively integration tests using public API
- Performance characteristics (parallelism)
- Error propagation through pipeline

### Coverage Goals
- 100% branch coverage
- All error conditions tested
- Edge cases (empty items, null values)

## File Structure

```
Simpipe.Net/
├── Blocks/
│   ├── ParallelBlock.cs
│   └── ParallelBuilder.cs
└── Pipes/
    ├── Pipe.cs (add Parallel method)
    └── ParallelPipeBuilder.cs

Simpipe.Net.Tests/
└── Pipes/
    └── ParallelPipeFixture.cs (all tests via public API)
```