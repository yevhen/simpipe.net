# Simpipe.Net

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A high-performance, composable pipeline pattern library for .NET using System.Threading.Channels. Build robust data processing pipelines with ease.

## Overview

Simpipe.Net provides a fluent API for constructing data processing pipelines using System.Threading.Channels. It simplifies complex data flow scenarios while maintaining high performance and reliability.

### Key Benefits

- **Composable**: Chain pipes together to create complex processing workflows
- **Concurrent**: Built with System.Threading.Channels for efficient parallel processing
- **Back-pressure Handling**: Automatic flow control prevents memory overload
- **Type-Safe**: Full generic type support with compile-time safety
- **Testable**: Clear separation of concerns makes testing straightforward

### When to Use Simpipe.Net

Use Simpipe.Net when you need:
- Stream processing with multiple transformation stages
- ETL (Extract, Transform, Load) pipelines
- Real-time data processing with batching capabilities
- Complex routing logic between processing stages
- Work-in-progress limiting

## Features

- ✅ Fluent API for pipeline construction
- ✅ Multiple specialized pipe types (Action, Batch)
- ✅ Async/await support throughout
- ✅ Automatic completion propagation
- ✅ Back-pressure handling via bounded capacity
- ✅ Conditional routing between pipes
- ✅ Work-in-progress limiting
- ✅ Performance monitoring (input/output/working counts)
- ✅ Cancellation token support

## Installation

```bash
dotnet add package Simpipe.Net
```

## Quick Start

Here's a simple example that demonstrates basic pipeline construction:

```csharp
using Simpipe.Pipes;

// Create a pipeline
var pipeline = new Pipeline<string>();

// Add an action pipe that processes each item
var processPipe = Pipe<string>
    .Action(item => Console.WriteLine($"Processing: {item}"))
    .Id("processor")
    .ToPipe();
pipeline.Add(processPipe);

// Add a batch pipe that groups items
var batchPipe = Pipe<string>
    .Batch(5, items => Console.WriteLine($"Batch of {items.Length} items"))
    .Id("batcher")
    .ToPipe();
pipeline.Add(batchPipe);

// Send items through the pipeline
await pipeline.Send("Item 1");
await pipeline.Send("Item 2");
await pipeline.Send("Item 3");
await pipeline.Send("Item 4");
await pipeline.Send("Item 5");

// Complete the pipeline
await pipeline.Complete();
```

## Core Concepts

### Pipes

Pipes are the fundamental building blocks of a pipeline. Each pipe:
- Receives items of type `T`
- Processes them according to its implementation
- Forwards results to the next pipe or routing target
- Tracks input, output, and working item counts via the `Block` property

### Pipeline

A Pipeline is a container that:
- Manages a sequence of connected pipes
- Handles automatic linking between pipes
- Provides completion tracking for the entire flow
- Allows sending items to specific pipes by ID

### Blocks

Blocks are the low-level processing units that power pipes:
- **ActionBlock**: Executes actions with configurable parallelism
- **BatchBlock**: Groups items into batches
- **BatchActionBlock**: Processes batches with parallelism
- **TimerBatchBlock**: Batches with time-based flushing
- **FilterBlock**: Filters items at the block level
- **ParallelBlock**: Manages fork-join parallel execution
- **NullBlock**: Discards items (sink)

### Routing and Filtering

Pipes support sophisticated flow control:
- **Filtering**: Use `.Filter()` to process only matching items
- **Routing**: Use `.LinkTo()` with predicates for conditional routing
- **Pass-through**: Non-matching filtered items automatically pass to next pipe
- **Fork-Join**: Split processing across parallel blocks then rejoin

### Completion

Simpipe.Net provides graceful shutdown:
- Call `Complete()` to signal no more items
- Completion propagates through the pipeline
- Use `await Completion` to wait for all processing

## Pipe Types

### ActionPipe

Executes an action for each item with configurable parallelism.

```csharp
var pipe = Pipe<Order>
    .Action(async order => {
        await ProcessOrder(order);
        Console.WriteLine($"Processed order {order.Id}");
    })
    .DegreeOfParallelism(4)
    .BoundedCapacity(100)
    .ToPipe();
```

### BatchPipe

Groups items into fixed-size batches with optional time-based triggers.

```csharp
var pipe = Pipe<LogEntry>
    .Batch(100, async entries => {
        await BulkInsertLogs(entries);
        Console.WriteLine($"Inserted {entries.Length} log entries");
    })
    .BatchTriggerPeriod(TimeSpan.FromSeconds(5)) // Flush incomplete batches after 5 seconds
    .ToPipe();
```

### PipelineLimiter

Limits work-in-progress items for flow control.

```csharp
var limiter = new PipelineLimiter<Order>(maxWork: 10, async order => {
    await ProcessOrder(order);
    // Signal completion
    await limiter.TrackDone(order);
});

// Send items with automatic back-pressure
await limiter.Send(order);

// Complete processing
await limiter.Complete();
```

### ForkPipe (Fork-Join Parallelism)

Execute multiple operations in parallel on the same item and wait for all to complete.

```csharp
// Define parallel operations
var validateBlock = Parallel<Order>
    .Action(async order => await ValidateOrder(order))
    .Id("validate")
    .DegreeOfParallelism(2);

var enrichBlock = Parallel<Order>
    .Action(async order => await EnrichOrderData(order))
    .Id("enrich")
    .DegreeOfParallelism(3);

var notifyBlock = Parallel<Order>
    .Action(async order => await NotifySubscribers(order))
    .Id("notify");

// Create fork-join pipe
var forkPipe = Pipe<Order>
    .Fork(validateBlock, enrichBlock, notifyBlock)
    .Join(order => Console.WriteLine($"All parallel operations completed for order {order.Id}"))
    .Id("fork-processor")
    .ToPipe();

// Items are processed by all blocks in parallel
await forkPipe.Send(order);

// The next pipe receives the item only after ALL parallel blocks complete
var nextPipe = Pipe<Order>.Action(SaveOrder).ToPipe();
forkPipe.LinkNext(nextPipe);
```

#### Parallel Batch Processing

You can also use batch operations within fork-join:

```csharp
var batchValidate = Parallel<Transaction>
    .Batch(100, async transactions => await BulkValidate(transactions))
    .Id("batch-validate")
    .BatchTriggerPeriod(TimeSpan.FromSeconds(5));

var batchAudit = Parallel<Transaction>
    .Batch(50, async transactions => await AuditLog(transactions))
    .Id("batch-audit");

var forkPipe = Pipe<Transaction>
    .Fork(batchValidate, batchAudit)
    .Id("transaction-processor")
    .ToPipe();
```

## Advanced Usage

### Custom Routing

Route items to different pipes based on conditions:

```csharp
var highPriorityPipe = CreateHighPriorityPipe();
var normalPipe = CreateNormalPipe();

sourcePipe.LinkTo(item => 
    item.Priority > 5 ? highPriorityPipe : normalPipe);
```

### Performance Monitoring

Track pipeline performance using the Block metrics:

```csharp
var pipe = Pipe<Data>
    .Action(ProcessData)
    .Id("processor")
    .ToPipe();

// Monitor performance
Console.WriteLine($"Input: {pipe.Block.InputCount}, Working: {pipe.Block.WorkingCount}, Output: {pipe.Block.OutputCount}");
```

### Cancellation

Support graceful cancellation:

```csharp
var cts = new CancellationTokenSource();

var pipe = Pipe<Item>
    .Action(async item => {
        await ProcessItem(item);
    })
    .Id("item-processor")
    .CancellationToken(cts.Token)
    .ToPipe();

// Cancel processing
cts.Cancel();
```

## Configuration Options

### Common Pipe Builder Methods

All pipe builders support these configuration methods:

- **`.Id(string)`**: Unique identifier for the pipe
- **`.Filter(Func<T, bool>)`**: Predicate to filter items (non-matching items pass through)
- **`.Route(Func<T, Pipe<T>>)`**: Function to determine routing target
- **`.ToPipe()`**: Build and return the configured pipe

### ActionPipe Builder Methods

```csharp
Pipe<T>.Action(action)
    .Id(string)                      // Pipe identifier
    .Filter(Func<T, bool>)           // Item filter
    .Route(Func<T, Pipe<T>>)         // Routing function
    .DegreeOfParallelism(int)        // Max concurrent executions (default: 1)
    .BoundedCapacity(int?)           // Max items buffered (default: parallelism * 2)
    .CancellationToken(token)        // Cancellation support
    .ToPipe()
```

### BatchPipe Builder Methods

```csharp
Pipe<T>.Batch(batchSize, action)
    .Id(string)                      // Pipe identifier
    .Filter(Func<T, bool>)           // Item filter
    .Route(Func<T, Pipe<T>>)         // Routing function
    .BatchTriggerPeriod(TimeSpan)    // Timer for incomplete batches
    .DegreeOfParallelism(int)        // Concurrent batch processing (default: 1)
    .BoundedCapacity(int?)           // Max items buffered (default: batchSize)
    .CancellationToken(token)        // Cancellation support
    .ToPipe()
```

### ForkPipe Builder Methods

```csharp
Pipe<T>.Fork(parallelBlocks...)
    .Id(string)                      // Pipe identifier
    .Filter(Func<T, bool>)           // Item filter
    .Route(Func<T, Pipe<T>>)         // Routing function
    .Join(Action<T>)                 // Action when all blocks complete
    .ToPipe()
```

### Parallel Block Builder Methods

```csharp
// For use within Fork pipes
Parallel<T>.Action(action)
    .Id(string)                      // Block identifier
    .Filter(Func<T, bool>)           // Item filter
    .DegreeOfParallelism(int)        // Max concurrent executions
    .BoundedCapacity(int?)           // Max items buffered
    .CancellationToken(token)        // Cancellation support

Parallel<T>.Batch(batchSize, action)
    .Id(string)                      // Block identifier
    .Filter(Func<T, bool>)           // Item filter
    .BatchTriggerPeriod(TimeSpan)    // Timer for incomplete batches
    .DegreeOfParallelism(int)        // Concurrent batch processing
    .BoundedCapacity(int?)           // Max items buffered
    .CancellationToken(token)        // Cancellation support
```

## Best Practices

1. **Always Complete Pipelines**: Call `Complete()` and await `Completion` to ensure graceful shutdown
   ```csharp
   await pipeline.Complete();
   await pipeline.Completion;
   ```

2. **Use Bounded Capacity**: Prevent memory issues by setting appropriate bounds
   ```csharp
   .BoundedCapacity(1000) // Limit to 1000 items
   ```

3. **Consider Batch Sizes**: For I/O operations, batch to reduce overhead
   ```csharp
   .Batch(100, items => BulkInsert(items)) // Process 100 at a time
   ```

4. **Monitor Performance**: Track pipeline metrics for optimization
   ```csharp
   var block = pipe.Block;
   if (block.InputCount > block.OutputCount * 2)
       Console.WriteLine("Potential bottleneck detected");
   ```

## Performance Considerations

### Memory Usage

- Use bounded capacity to limit memory consumption
- Consider item size when setting batch sizes
- Monitor `WorkingCount` to identify memory pressure

### Parallelism Tuning

- Set `DegreeOfParallelism` based on workload type
- CPU-bound: Use `Environment.ProcessorCount`
- I/O-bound: Can use higher values

### Batch Size Optimization

- Larger batches reduce overhead but increase latency
- Smaller batches improve responsiveness but increase overhead
- Consider your workload characteristics when choosing batch sizes

## Examples

### Data Processing Pipeline

```csharp
var pipeline = new Pipeline<DataRecord>();

// Parse stage
pipeline.Add(Pipe<DataRecord>
    .Action(record => record.Parse())
    .Id("parser")
    .DegreeOfParallelism(4)
    .ToPipe());

// Validate stage
pipeline.Add(Pipe<DataRecord>
    .Action(record => {
        if (!record.IsValid)
            throw new ValidationException($"Invalid record: {record.Id}");
    })
    .Id("validator")
    .Filter(record => record.RequiresValidation)
    .ToPipe());

// Batch for database insert
pipeline.Add(Pipe<DataRecord>
    .Batch(1000, async records => {
        await BulkInsertToDatabase(records);
    })
    .Id("database-writer")
    .BatchTriggerPeriod(TimeSpan.FromSeconds(10))
    .ToPipe());
```

### Real-time Stream Processing

```csharp
var pipeline = new Pipeline<StreamEvent>();

// Filter stage
var filterPipe = Pipe<StreamEvent>
    .Action(_ => { })
    .Id("filter")
    .Filter(evt => evt.Severity >= Severity.Warning)
    .ToPipe();

// Route by severity
var criticalPipe = CreateCriticalAlertPipe();
var warningPipe = CreateWarningLogPipe();

filterPipe.LinkTo(evt => 
    evt.Severity == Severity.Critical ? criticalPipe : warningPipe);

pipeline.Add(filterPipe);
```

## Building and Testing

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Test Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Contributing

We welcome contributions! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Style

- Follow C# coding conventions
- Use meaningful names
- Write unit tests for new features
- Ensure all tests pass before submitting PR

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.