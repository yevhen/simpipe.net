# Parallel Enrichment Implementation Plan

## Architecture Overview

The parallel enrichment feature will be implemented using a `ParallelBlock<T>` that implements `IActionBlock<T>`. This block will coordinate multiple child blocks (enrichments) and ensure responses are applied serially to avoid concurrent mutations.

### Key Components

1. **ParallelBlock<T>** - Coordinates parallel enrichment execution
2. **ParallelBuilder<T>** - Entry point for fluent API
3. **ActionEnrichmentBuilder<T, TRequest, TResponse>** - Fluent builder for action enrichments
4. **BatchEnrichmentBuilder<T, TRequest, TResponse>** - Fluent builder for batch enrichments
5. **ActionEnrichment<T, TRequest, TResponse>** - Configuration for action enrichment
6. **BatchEnrichment<T, TRequest, TResponse>** - Configuration for batch enrichment
7. **EnrichmentContext<T>** - Tracks item and collected responses

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
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Extract(item => item.Id)
            .Execute(async id => { await Task.Delay(1); return id * 2; })
            .Apply((item, result) => { item.EnrichedValue = result; enriched = true; })
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    await block.Send(new BlockItem<TestItem>(item));
    await block.Complete();
    
    Assert.That(enriched, Is.True);
    Assert.That(item.EnrichedValue, Is.EqualTo(2));
}
```

**Implementation**:
- Create `ParallelBlock<T>` with minimal Send/Complete
- Create `ParallelBuilder<T>` with Action method returning ActionEnrichmentBuilder
- Create `ActionEnrichmentBuilder<T, TRequest, TResponse>` with fluent Extract/Execute/Apply
- Create `ActionEnrichment<T, TRequest, TResponse>` to hold configuration
- Implement basic extract → execute → apply flow

### Increment 2: Multiple Parallel Enrichments
**Outcome**: Execute multiple enrichments in parallel, apply serially

**Test First**:
```csharp
[Test] 
public async Task Should_execute_multiple_enrichments_in_parallel()
{
    var item = new TestItem { Id = 1, Text = "hello" };
    var executionOrder = new ConcurrentBag<string>();
    var applicationOrder = new List<string>();
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Extract(item => item.Id)
            .Execute(async id => { 
                executionOrder.Add("id");
                await Task.Delay(50);
                return id * 2;
            })
            .Apply((item, result) => {
                applicationOrder.Add("id");
                item.EnrichedId = result;
            })
        .Action<string, int>()
            .Extract(item => item.Text)
            .Execute(async text => {
                executionOrder.Add("text");
                await Task.Delay(10);
                return text.Length;
            })
            .Apply((item, result) => {
                applicationOrder.Add("text");
                item.TextLength = result;
            })
        .Build();
    
    // Execute
    var block = new ParallelBlock<TestItem>(parallel);
    await block.Send(new BlockItem<TestItem>(item));
    await block.Complete();
    
    // Verify parallel execution (both started before either finished)
    Assert.That(executionOrder.Count, Is.EqualTo(2));
    
    // Verify serial application
    Assert.That(applicationOrder, Is.EqualTo(new[] { "id", "text" }));
    Assert.That(item.EnrichedId, Is.EqualTo(2));
    Assert.That(item.TextLength, Is.EqualTo(5));
}
```

**Implementation**:
- Store multiple enrichments in ParallelBuilder
- Execute all enrichments concurrently using Task.WhenAll
- Collect responses and apply in order

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
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<string, int>()
            .Extract(item => item.Text)
            .Execute(async text => { 
                executedFor.Add(items.First(i => i.Text == text).Id);
                return text.Length;
            })
            .Apply((item, result) => item.TextLength = result)
            .Filter(item => !string.IsNullOrEmpty(item.Text))
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    foreach (var item in items)
        await block.Send(new BlockItem<TestItem>(item));
    await block.Complete();
    
    Assert.That(executedFor, Is.EqualTo(new[] { 1 }));
    Assert.That(items[0].TextLength, Is.EqualTo(5));
    Assert.That(items[1].TextLength, Is.EqualTo(0));
}
```

**Implementation**:
- Add Filter method to enrichment builder (consistent with ActionPipeBuilder)
- Check condition before extraction
- Skip enrichment if condition is false

### Increment 4: Fail-Fast Error Handling
**Outcome**: Cancel all enrichments on first failure

**Test First**:
```csharp
[Test]
public async Task Should_cancel_all_enrichments_on_first_failure()
{
    var item = new TestItem();
    var started = new ConcurrentBag<string>();
    var completed = new ConcurrentBag<string>();
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Extract(_ => 1)
            .Execute(async _ => {
                started.Add("fast");
                await Task.Delay(10);
                throw new InvalidOperationException("fast failed");
            })
            .Apply((_, __) => completed.Add("fast"))
        .Action<int, int>()
            .Extract(_ => 2)
            .Execute(async _ => {
                started.Add("slow");
                await Task.Delay(100);
                completed.Add("slow");
                return 42;
            })
            .Apply((_, __) => completed.Add("slow-applied"))
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    
    var ex = Assert.ThrowsAsync<InvalidOperationException>(
        async () => await block.Send(new BlockItem<TestItem>(item)));
    
    Assert.That(ex.Message, Is.EqualTo("fast failed"));
    Assert.That(started.Count, Is.EqualTo(2)); // Both started
    Assert.That(completed.Count, Is.EqualTo(0)); // Neither completed
}
```

**Implementation**:
- Use CancellationTokenSource for coordinated cancellation
- Cancel all tasks on first exception
- Propagate first exception to caller

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
    
    var parallel = new ParallelBuilder<TestItem>()
        .Batch<Dictionary<int, string>, Dictionary<int, int>>()
            .ExtractBatch(items => items.ToDictionary(i => i.Id, i => i.Text))
            .Execute(async dict => {
                await Task.Delay(10);
                return dict.ToDictionary(kv => kv.Key, kv => kv.Value.Length);
            })
            .ApplyBatch((items, result) => {
                foreach (var item in items)
                    item.BatchLength = result[item.Id];
            })
            .BatchSize(2)
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    
    // Send 3 items - should trigger 2 batches
    foreach (var item in items)
        await block.Send(new BlockItem<TestItem>(item));
    await block.Complete();
    
    Assert.That(items[0].BatchLength, Is.EqualTo(5));
    Assert.That(items[1].BatchLength, Is.EqualTo(5));
    Assert.That(items[2].BatchLength, Is.EqualTo(4));
}
```

**Implementation**:
- Create `BatchEnrichmentBuilder<T, TRequest, TResponse>` class
- Create `BatchEnrichment<T, TRequest, TResponse>` class  
- Use internal BatchBlock to accumulate items
- Execute batch when size reached
- Apply results to all items in batch

### Increment 6: Batch Timeout Support
**Outcome**: Flush incomplete batches after timeout

**Test First**:
```csharp
[Test]
public async Task Should_flush_batch_on_timeout()
{
    var item = new TestItem { Id = 1 };
    var batchExecuted = false;
    
    var parallel = new ParallelBuilder<TestItem>()
        .Batch<List<int>, Dictionary<int, int>>()
            .ExtractBatch(items => items.Select(i => i.Id).ToList())
            .Execute(async ids => {
                batchExecuted = true;
                return ids.ToDictionary(id => id, id => id * 10);
            })
            .ApplyBatch((items, result) => {
                foreach (var item in items)
                    item.BatchResult = result[item.Id];
            })
            .BatchSize(10)
            .BatchTriggerPeriod(TimeSpan.FromMilliseconds(50))
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    await block.Send(new BlockItem<TestItem>(item));
    
    // Wait for timeout
    await Task.Delay(100);
    
    Assert.That(batchExecuted, Is.True);
    Assert.That(item.BatchResult, Is.EqualTo(10));
}
```

**Implementation**:
- Use TimerBatchBlock instead of BatchBlock
- Configure timeout parameter
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
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Extract(item => item.Id)
            .Execute(async id => {
                Interlocked.Increment(ref currentConcurrency);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
                await Task.Delay(50);
                Interlocked.Decrement(ref currentConcurrency);
                return id;
            })
            .Apply((item, result) => item.Result = result)
            .DegreeOfParallelism(2)
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    
    // Send 5 items
    for (var i = 1; i <= 5; i++)
        await block.Send(new BlockItem<TestItem>(new TestItem { Id = i }));
    await block.Complete();
    
    Assert.That(maxConcurrency, Is.LessThanOrEqualTo(2));
    Assert.That(maxConcurrency, Is.GreaterThanOrEqualTo(1));
}
```

**Implementation**:
- Pass parallelism parameter to underlying ActionBlock
- Default to 1 if not specified

### Increment 8: Counter Support
**Outcome**: Expose counters for monitoring

**Test First**:
```csharp
[Test]
public async Task Should_expose_enrichment_counters()
{
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Id("multiply")
            .Extract(item => item.Id)
            .Execute(async id => { await Task.Delay(1); return id * 2; })
            .Apply((item, result) => item.Result = result)
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    
    await block.Send(new BlockItem<TestItem>(new TestItem { Id = 1 }));
    await block.Send(new BlockItem<TestItem>(new TestItem { Id = 2 }));
    await block.Complete();
    
    var counter = block.GetEnrichmentCounter("multiply");
    Assert.That(counter.InputCount, Is.EqualTo(0));
    Assert.That(counter.OutputCount, Is.EqualTo(2));
    Assert.That(counter.WorkingCount, Is.EqualTo(0));
}
```

**Implementation**:
- Store enrichment names
- Expose GetEnrichmentCounter method
- Return IItemCounter from child blocks

### Increment 9: Integration with Pipe API
**Outcome**: Wire ParallelBlock through Pipe.Parallel() API

**Test First**:
```csharp
[Test]
public async Task Should_integrate_with_pipe_api()
{
    var results = new List<TestItem>();
    
    var pipe = Pipe<TestItem>
        .Parallel(p => p
            .Action<int, int>()
                .Extract(item => item.Id)
                .Execute(async id => { await Task.Delay(1); return id * 10; })
                .Apply((item, result) => item.EnrichedId = result))
        .Id("enricher")
        .ToPipe();
    
    var collector = PipeMock<TestItem>.Create("collector", item => results.Add(item));
    pipe.LinkNext(collector);
    
    await pipe.Send(new TestItem { Id = 5 });
    pipe.Complete();
    await pipe.Completion;
    
    Assert.That(results.Count, Is.EqualTo(1));
    Assert.That(results[0].EnrichedId, Is.EqualTo(50));
    
    // Verify we can cast to ParallelBlock
    var parallelBlock = pipe.Block as ParallelBlock<TestItem>;
    Assert.That(parallelBlock, Is.Not.Null);
}
```

**Implementation**:
- Add Parallel method to Pipe<T> class
- Return builder that creates ParallelBlock
- Ensure proper integration with PipeOptions

### Increment 10: Cancellation Token Support
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
    
    var parallel = new ParallelBuilder<TestItem>()
        .Action<int, int>()
            .Extract(_ => 1)
            .Execute(async _ => {
                await Task.Delay(100, cts1.Token);
                executed1 = true;
                return 1;
            })
            .Apply((_, __) => { })
            .CancellationToken(cts1.Token)
        .Action<int, int>()
            .Extract(_ => 2)
            .Execute(async _ => {
                await Task.Delay(100, cts2.Token);
                executed2 = true;
                return 2;
            })
            .Apply((_, __) => { })
            .CancellationToken(cts2.Token)
        .Build();
    
    var block = new ParallelBlock<TestItem>(parallel);
    
    // Cancel first enrichment
    cts1.Cancel();
    
    // Should throw because one enrichment was cancelled
    Assert.ThrowsAsync<OperationCanceledException>(async () => {
        await block.Send(new BlockItem<TestItem>(new TestItem()));
    });
    
    Assert.That(executed1, Is.False);
    Assert.That(executed2, Is.False); // Also cancelled due to fail-fast
}
```

**Implementation**:
- Add CancellationToken to enrichment configuration
- Create linked token source for fail-fast behavior
- Pass tokens to child blocks

## Testing Strategy

### Unit Tests
- Each increment has focused unit tests
- Test both success and error paths
- Use minimal test items/mocks

### Integration Tests
- End-to-end pipeline with ParallelBlock
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
│   ├── ParallelBuilder.cs
│   ├── ActionEnrichmentBuilder.cs
│   ├── BatchEnrichmentBuilder.cs
│   ├── ActionEnrichment.cs
│   ├── BatchEnrichment.cs
│   └── EnrichmentContext.cs
└── Pipes/
    └── Pipe.cs (add Parallel method)

Simpipe.Net.Tests/
├── Blocks/
│   ├── ParallelBlockFixture.cs
│   ├── ParallelBuilderFixture.cs
│   ├── ActionEnrichmentBuilderFixture.cs
│   └── BatchEnrichmentBuilderFixture.cs
└── Pipes/
    └── ParallelPipeFixture.cs (integration tests)
```