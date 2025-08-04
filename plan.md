# Simpipe.Net Migration Plan: From TPL Dataflow to .NET Channels

## Design Philosophy

The current C# implementation is tightly coupled to TPL Dataflow with complex inheritance hierarchies. The Go version demonstrates a cleaner approach using channels as the communication primitive. We'll migrate to a similar design using .NET's System.Threading.Channels.

### Core Design Principles

1. **Simplicity First**: Each component should do one thing well
2. **Composition over Inheritance**: Use interfaces and composition
3. **Channels + Done Callbacks**: Blocks read from channels, call Done when finished
4. **Test-First Development**: Write minimal tests with 100% branch coverage
5. **Atomic Increments**: Each increment delivers working functionality

### Architecture Overview

```
    Input       Block 1         Block 2         Block 3
   Channel   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
      │      │  Action +   │ │  Action +   │ │  Action +   │  
      └─────▶│  Done()     │─│  Done()     │─│  Done()     │──▶ Final
             └─────────────┘ └─────────────┘ └─────────────┘
                   │               │               │
                   ▼               ▼               ▼
               Channel<T>      Channel<T>      Channel<T>
```

Each block:
- Reads from Input ChannelReader<T>
- Executes Action<T> (processes item)
- Calls Done<T> callback (sends to next block)

### Key Architectural Insights from Go

1. **Separation of Concerns**: 
   - BatchBlock ONLY batches (T → T[])  
   - ActionBlock ONLY processes (T → T)
   - BatchActionBlock COMPOSES both with intermediate channel

2. **No Output Channels**: Blocks use Done callbacks instead of output channels

3. **Composition Pattern**: Complex blocks are built by composing simple blocks with intermediate channels

4. **Uniform Interface**: All blocks have same pattern: Input + Action + Done

## Implementation Increments

### Increment 1: Core Channel-Based Action Block with Done Callback
**Outcome**: A working action block that processes items from a channel and calls Done

**Test First**:
```csharp
[Test]
public async Task ActionBlock_ProcessesSingleItem()
{
    var input = Channel.CreateUnbounded<int>();
    var processed = 0;
    var completed = 0;
    
    var block = new ActionBlock<int>(
        input.Reader,
        action: item => processed = item,
        done: item => completed = item);
    
    await input.Writer.WriteAsync(42);
    input.Writer.Complete();
    
    await block.RunAsync();
    
    Assert.AreEqual(42, processed);
    Assert.AreEqual(42, completed);
}
```

**Implementation**: ActionBlock with ChannelReader<T>, Action<T>, and Done<T> callback

---

### Increment 2: Parallel Processing Support
**Outcome**: ActionBlock can process items in parallel

**Test First**:
```csharp
[Test]
public async Task ActionBlock_ProcessesInParallel()
{
    var channel = Channel.CreateUnbounded<int>();
    var processedCount = 0;
    var maxConcurrency = 0;
    var currentConcurrency = 0;
    
    var block = new ActionBlock<int>(
        channel.Reader,
        async item => {
            Interlocked.Increment(ref currentConcurrency);
            maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
            await Task.Delay(50);
            Interlocked.Increment(ref processedCount);
            Interlocked.Decrement(ref currentConcurrency);
        },
        parallelism: 3);
    
    // Write 5 items
    for (int i = 0; i < 5; i++)
        await channel.Writer.WriteAsync(i);
    channel.Writer.Complete();
    
    await block.RunAsync();
    
    Assert.AreEqual(5, processedCount);
    Assert.GreaterOrEqual(maxConcurrency, 2);
}
```

**Implementation**: Add parallelism parameter and concurrent processing

---

### Increment 3: Basic Pipeline with Linking via Done Callback
**Outcome**: Can link two blocks together using Done callbacks

**Test First**:
```csharp
[Test]
public async Task Pipeline_LinksTwoBlocks()
{
    var input = Channel.CreateUnbounded<int>();
    var intermediate = Channel.CreateUnbounded<int>();
    var result = 0;
    
    // First block: multiply by 2, send to intermediate channel
    var multiply = new ActionBlock<int>(
        input.Reader,
        item => { /* transform in-place */ item *= 2; },
        done: item => intermediate.Writer.WriteAsync(item));
        
    // Second block: store result
    var store = new ActionBlock<int>(
        intermediate.Reader,
        item => result = item);
    
    await input.Writer.WriteAsync(21);
    input.Writer.Complete();
    
    await Task.WhenAll(multiply.RunAsync(), store.RunAsync());
    
    Assert.AreEqual(42, result);
}
```

**Implementation**: Blocks have Done callback parameter for linking

---

### Increment 4: Transform Block (In-Place Mutation)
**Outcome**: A block that transforms items in-place (following Go pattern)

**Test First**:
```csharp
[Test]
public async Task TransformBlock_MutatesInPlace()
{
    var input = Channel.CreateUnbounded<TestItem>();
    var final = Channel.CreateUnbounded<TestItem>();
    var result = new List<TestItem>();
    
    var transform = new ActionBlock<TestItem>(
        input.Reader,
        action: item => item.Value *= 2,
        done: item => final.Writer.WriteAsync(item));
        
    var collect = new ActionBlock<TestItem>(
        final.Reader,
        action: item => result.Add(item));
    
    await input.Writer.WriteAsync(new TestItem { Value = 21 });
    input.Writer.Complete();
    
    await Task.WhenAll(transform.RunAsync(), collect.RunAsync());
    
    Assert.AreEqual(42, result[0].Value);
}
```

**Implementation**: Transform by mutating items in-place (simpler than generic transform)

---

### Increment 5: Pure Batch Block (T → T[])
**Outcome**: A block that ONLY batches items, doesn't process them

**Test First**:
```csharp
[Test]
public async Task BatchBlock_OnlyCreatesBatches()
{
    var input = Channel.CreateUnbounded<int>();
    var batches = new List<int[]>();
    
    // BatchBlock ONLY batches - no action processing
    var batchBlock = new BatchBlock<int>(
        input.Reader,
        batchSize: 3,
        done: batch => batches.Add(batch)); // Done called with T[]
    
    for (int i = 1; i <= 7; i++)
        await input.Writer.WriteAsync(i);
    input.Writer.Complete();
    
    await batchBlock.RunAsync();
    
    Assert.AreEqual(3, batches.Count);
    Assert.AreEqual(new[] {1, 2, 3}, batches[0]);
    Assert.AreEqual(new[] {4, 5, 6}, batches[1]); 
    Assert.AreEqual(new[] {7}, batches[2]);
}
```

**Implementation**: Pure batching block with Done<T[]> callback (no action processing)

---

### Increment 6: Time-Based Batch Flushing
**Outcome**: BatchBlock flushes incomplete batches after timeout

**Test First**:
```csharp
[Test]
public async Task BatchBlock_FlushesOnTimeout()
{
    var input = Channel.CreateUnbounded<int>();
    var batches = new List<int[]>();
    
    var batch = new BatchBlock<int>(
        input.Reader,
        batchSize: 10,
        flushInterval: TimeSpan.FromMilliseconds(100),
        done: b => batches.Add(b));
    
    await input.Writer.WriteAsync(1);
    await input.Writer.WriteAsync(2);
    
    var batchTask = batch.RunAsync();
    
    await Task.Delay(150);
    input.Writer.Complete();
    
    await batchTask;
    
    Assert.AreEqual(1, batches.Count); 
    Assert.AreEqual(new[] {1, 2}, batches[0]);
}
```

**Implementation**: Add timer-based flushing to BatchBlock

---

### Increment 7: Composite BatchActionBlock
**Outcome**: A block that combines BatchBlock + ActionBlock with intermediate channel

**Test First**:
```csharp
[Test]
public async Task BatchActionBlock_ProcessesBatchesWithAction()
{
    var input = Channel.CreateUnbounded<int>();
    var processedItems = new List<int>();
    
    // Composite: batches items, then processes each batch, then unpacks to individual items
    var batchAction = new BatchActionBlock<int>(
        input.Reader,
        batchSize: 3,
        parallelism: 2,
        batchAction: batch => {
            // Process the entire batch (e.g., bulk database operation)
            Console.WriteLine($"Processing batch of {batch.Length}");
            foreach (var item in batch)
                item *= 10; // Transform each item in batch
        },
        done: item => processedItems.Add(item)); // Called for each individual item
    
    for (int i = 1; i <= 7; i++)
        await input.Writer.WriteAsync(i);
    input.Writer.Complete();
    
    await batchAction.RunAsync();
    
    Assert.AreEqual(7, processedItems.Count);
    Assert.Contains(10, processedItems); // 1 * 10
    Assert.Contains(20, processedItems); // 2 * 10  
    Assert.Contains(70, processedItems); // 7 * 10
}
```

**Implementation**: Composite block that internally creates BatchBlock + ActionBlock with intermediate channel

---

### Increment 8: Pipeline Builder with Channel Management
**Outcome**: Builder automatically creates and links channels between blocks

**Test First**:
```csharp
[Test]
public async Task PipelineBuilder_CreatesLinkedPipeline()
{
    var results = new List<int>();
    
    var pipeline = new PipelineBuilder<int>()
        .Action(x => x *= 2)        // Transform in-place
        .Action(x => x += 10)       // Transform in-place  
        .Action(x => results.Add(x)) // Final action
        .Build();
    
    await pipeline.SendAsync(21);
    await pipeline.CompleteAsync();
    
    Assert.AreEqual(52, results[0]); // (21 * 2) + 10 = 52
}
```

**Implementation**: Builder creates intermediate channels and links Done callbacks

---

### Increment 9: Conditional Routing
**Outcome**: Route items to different blocks based on predicate

**Test First**:
```csharp
[Test]
public async Task Router_RoutesBasedOnPredicate()
{
    var evens = new List<int>();
    var odds = new List<int>();
    
    var pipeline = new PipelineBuilder<int>()
        .Route(x => x % 2 == 0,
            even => even.Action(x => evens.Add(x)),
            odd => odd.Action(x => odds.Add(x)))
        .Build();
    
    for (int i = 1; i <= 5; i++)
        await pipeline.SendAsync(i);
    await pipeline.CompleteAsync();
    
    Assert.AreEqual(new[] { 2, 4 }, evens);
    Assert.AreEqual(new[] { 1, 3, 5 }, odds);
}
```

**Implementation**: Routing block with predicate-based distribution

---

### Increment 9: Error Handling
**Outcome**: Graceful error handling with configurable retry/dlq

**Test First**:
```csharp
[Test]
public async Task Pipeline_HandlesErrors()
{
    var processed = new List<int>();
    var errors = new List<(int item, Exception error)>();
    
    var pipeline = new PipelineBuilder<int>()
        .Action(x => {
            if (x == 3) throw new InvalidOperationException("Three!");
            processed.Add(x);
        })
        .OnError((item, ex) => errors.Add((item, ex)))
        .Build();
    
    for (int i = 1; i <= 5; i++)
        await pipeline.SendAsync(i);
    await pipeline.CompleteAsync();
    
    Assert.AreEqual(new[] { 1, 2, 4, 5 }, processed);
    Assert.AreEqual(1, errors.Count);
    Assert.AreEqual(3, errors[0].item);
}
```

**Implementation**: Error handling with callbacks

---

### Increment 10: Bounded Channels & Backpressure
**Outcome**: Support for bounded channels and backpressure

**Test First**:
```csharp
[Test]
public async Task Pipeline_HandlesBoundedCapacity()
{
    var slowProcessed = 0;
    
    var pipeline = new PipelineBuilder<int>()
        .WithBoundedCapacity(2)
        .Action(async x => {
            await Task.Delay(100);
            Interlocked.Increment(ref slowProcessed);
        })
        .Build();
    
    // This should block after 2 items
    var sendTasks = new List<Task>();
    for (int i = 0; i < 5; i++)
    {
        sendTasks.Add(pipeline.SendAsync(i));
    }
    
    // Not all sends complete immediately
    var completed = sendTasks.Count(t => t.IsCompleted);
    Assert.Less(completed, 5);
    
    await pipeline.CompleteAsync();
    Assert.AreEqual(5, slowProcessed);
}
```

**Implementation**: Bounded channel support

---

### Increment 11: Dynamic Batching
**Outcome**: Batch size adjusts based on throughput

**Test First**:
```csharp
[Test]
public async Task DynamicBatch_AdjustsBatchSize()
{
    var batchSizes = new List<int>();
    
    var pipeline = new PipelineBuilder<int>()
        .DynamicBatch()
        .Action(batch => batchSizes.Add(batch.Length))
        .Build();
    
    // Fast burst
    for (int i = 0; i < 100; i++)
        await pipeline.SendAsync(i);
    
    await Task.Delay(200);
    
    // Slow trickle
    for (int i = 0; i < 10; i++)
    {
        await pipeline.SendAsync(i);
        await Task.Delay(50);
    }
    
    await pipeline.CompleteAsync();
    
    // Should have larger batches during burst
    var firstBatches = batchSizes.Take(3).Average();
    var lastBatches = batchSizes.Skip(Math.Max(0, batchSizes.Count - 3)).Average();
    
    Assert.Greater(firstBatches, lastBatches);
}
```

**Implementation**: Adaptive batch sizing based on arrival rate

---

### Increment 13: Metrics & Monitoring
**Outcome**: Built-in metrics for monitoring pipeline health

**Test First**:
```csharp
[Test]
public async Task Pipeline_TracksMetrics()
{
    var pipeline = new PipelineBuilder<int>()
        .Action(async x => await Task.Delay(10))
        .Build();
    
    for (int i = 0; i < 10; i++)
        await pipeline.SendAsync(i);
    
    var metrics = pipeline.GetMetrics();
    Assert.AreEqual(10, metrics.ItemsReceived);
    Assert.Greater(metrics.ItemsProcessing, 0);
    
    await pipeline.CompleteAsync();
    
    metrics = pipeline.GetMetrics();
    Assert.AreEqual(10, metrics.ItemsCompleted);
    Assert.AreEqual(0, metrics.ItemsProcessing);
}
```

**Implementation**: Metrics tracking throughout pipeline

## Migration Strategy

1. **New Namespace**: Create `Simpipe.Channels` namespace for new implementation
2. **Side-by-Side**: Both implementations coexist during migration
3. **Adapter Pattern**: Create adapters to use new blocks in old pipelines
4. **Gradual Migration**: Migrate one component at a time
5. **Deprecation**: Mark old components as obsolete once stable

## Testing Philosophy

- Each test covers exactly one behavior
- Tests are independent and can run in any order
- Use real time delays sparingly (prefer TaskCompletionSource)
- Mock external dependencies
- Test names describe expected behavior
- Arrange-Act-Assert pattern
- No test should take more than 100ms

## Success Criteria

1. All existing functionality is preserved
2. Performance is equal or better
3. Code complexity is reduced by 50%
4. 100% branch coverage on all new code
5. Memory usage is reduced due to simpler design
6. API is more intuitive and easier to use

## Non-Goals

1. Backward compatibility (this is a major version change)
2. Feature parity with TPL Dataflow (we want simplicity)
3. Supporting every edge case (80/20 rule)

## Timeline

Each increment should take approximately 1-2 hours to implement with tests. The entire migration can be completed in 2-3 days of focused work.

Total: 13 increments = ~13-26 hours of implementation time.