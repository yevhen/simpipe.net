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
- Tracks input, output, and working item counts

### Pipeline

A Pipeline is a container that:
- Manages a sequence of connected pipes
- Handles automatic linking between pipes
- Provides completion tracking for the entire flow
- Allows sending items to specific pipes by ID

### Routing

Pipes support conditional routing:
- Route items based on predicates
- Link to multiple downstream pipes
- Work-in-progress flow control

### Completion

Simpipe.Net provides graceful shutdown:
- Call `Complete()` to signal no more items
- Completion propagates through the pipeline
- Use `await Completion` to wait for all processing

## Pipe Types

### ActionPipe

Executes an action for each item with configurable parallelism.

```csharp
var pipe = new PipeBuilder<Order>()
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
var pipe = new PipeBuilder<LogEntry>()
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

Track pipeline performance using the ItemCounter interface:

```csharp
var pipe = Pipe<Data>
    .Action(ProcessData)
    .Id("processor")
    .ToPipe();

// Monitor performance
var counter = pipe.ItemCounter;
Console.WriteLine($"Input: {counter.InputCount}, Working: {counter.WorkingCount}, Output: {counter.OutputCount}");
```

### Performance Monitoring

Monitor pipeline performance in real-time:

```csharp
var timer = new Timer(_ => {
    foreach (var pipe in pipeline)
    {
        Console.WriteLine($"{pipe.Id}: " +
            $"Input={pipe.InputCount}, " +
            $"Working={pipe.WorkingCount}, " +
            $"Output={pipe.OutputCount}");
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
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

### Common Options

All pipes support these configuration options:

- **Id**: Unique identifier for the pipe
- **Filter**: Predicate to filter items
- **Route**: Function to determine routing
- **CancellationToken**: Token for cancellation

### ActionPipe Options

- **DegreeOfParallelism**: Maximum concurrent executions
- **BoundedCapacity**: Maximum items in the pipe
- **Action**: The action to execute

### BatchPipe Options

- **BatchSize**: Number of items per batch
- **BatchTriggerPeriod**: Time to wait before flushing incomplete batch
- **BoundedCapacity**: Maximum items buffered
- **DegreeOfParallelism**: Concurrent batch processing

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
   var counter = pipe.ItemCounter;
   if (counter.InputCount > counter.OutputCount * 2)
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