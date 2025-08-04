# Simpipe.Net

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A high-performance, composable pipeline pattern library for .NET using TPL Dataflow. Build robust data processing pipelines with ease.

## Overview

Simpipe.Net provides a fluent API for constructing data processing pipelines on top of TPL Dataflow. It simplifies complex data flow scenarios while maintaining the performance and reliability of the underlying dataflow architecture.

### Key Benefits

- **Composable**: Chain pipes together to create complex processing workflows
- **Concurrent**: Built on TPL Dataflow for efficient parallel processing
- **Back-pressure Handling**: Automatic flow control prevents memory overload
- **Type-Safe**: Full generic type support with compile-time safety
- **Testable**: Clear separation of concerns makes testing straightforward

### When to Use Simpipe.Net

Use Simpipe.Net when you need:
- Stream processing with multiple transformation stages
- ETL (Extract, Transform, Load) pipelines
- Real-time data processing with batching capabilities
- Complex routing logic between processing stages
- Integration with existing TPL Dataflow blocks

## Features

- ✅ Fluent API for pipeline construction
- ✅ Multiple specialized pipe types (Action, Batch, DynamicBatch, Group)
- ✅ Async/await support throughout
- ✅ Automatic completion propagation
- ✅ Back-pressure handling via bounded capacity
- ✅ Conditional routing between pipes
- ✅ Integration with existing TPL Dataflow blocks
- ✅ Performance monitoring (input/output/working counts)
- ✅ Cancellation token support

## Installation

```bash
dotnet add package Simpipe.Net
```

## Quick Start

Here's a simple example that demonstrates basic pipeline construction:

```csharp
using Youscan.Core.Pipes;

// Create a pipeline builder
var builder = new PipeBuilder<string>();

// Create a pipeline
var pipeline = new Pipeline<string>();

// Add an action pipe that processes each item
var processPipe = builder
    .Action(item => Console.WriteLine($"Processing: {item}"))
    .ToPipe();
pipeline.Add(processPipe);

// Add a batch pipe that groups items
var batchPipe = builder
    .Batch(5, items => Console.WriteLine($"Batch of {items.Length} items"))
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
- Integrate with TPL Dataflow blocks

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

### DynamicBatchPipe

Adaptively batches items based on throughput, ideal for variable workloads.

```csharp
var pipe = new PipeBuilder<Message>()
    .DynamicBatch(async messages => {
        await SendMessageBatch(messages);
    })
    .MaxBatchSize(1000)
    .InitialBatchInterval(TimeSpan.FromMilliseconds(100))
    .MaxBatchInterval(TimeSpan.FromSeconds(1))
    .ToPipe();
```

### GroupPipe

Distributes items across multiple child pipes for parallel processing paths.

```csharp
var groupPipe = new GroupPipeOptions<Request>()
    .BoundedCapacity(1000)
    .ToPipe();

// Add child pipes with optional predicates
var apiPipe = CreateApiPipe();
var dbPipe = CreateDatabasePipe();

groupPipe.Add(apiPipe, (request, pipe) => request.Type == RequestType.Api);
groupPipe.Add(dbPipe, (request, pipe) => request.Type == RequestType.Database);

pipeline.Add(groupPipe);
```

### BlockPipeAdapter

Wraps existing TPL Dataflow blocks as pipes.

```csharp
var transformBlock = new TransformBlock<int, string>(
    n => n.ToString(),
    new ExecutionDataflowBlockOptions { 
        MaxDegreeOfParallelism = 2 
    });

var adapter = new BlockPipeAdapter<string>(
    new PipeOptions<string>().Id("transformer"),
    transformBlock);

pipeline.Add(adapter);
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

### Integration with TPL Dataflow

Seamlessly integrate with existing dataflow blocks:

```csharp
// Link pipe to dataflow block
var transformBlock = new TransformBlock<Data, Result>(Transform);
pipe.LinkTo(transformBlock);

// Link dataflow block to pipe
var targetPipe = CreateTargetPipe();
transformBlock.LinkTo(targetPipe.Target);
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

var pipe = new PipeBuilder<Item>()
    .Action(async item => {
        await ProcessItem(item);
    })
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

4. **Use DynamicBatchPipe for Variable Workloads**: It adapts to throughput changes
   ```csharp
   .DynamicBatch(items => ProcessBatch(items))
   ```

5. **Monitor Performance**: Track pipeline metrics for optimization
   ```csharp
   if (pipe.InputCount > pipe.OutputCount * 2)
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
- Use `DynamicBatchPipe` when optimal size varies

## Examples

### Data Processing Pipeline

```csharp
var pipeline = new Pipeline<DataRecord>();

// Parse stage
pipeline.Add(new PipeBuilder<DataRecord>()
    .Action(record => record.Parse())
    .DegreeOfParallelism(4)
    .ToPipe());

// Validate stage
pipeline.Add(new PipeBuilder<DataRecord>()
    .Action(record => {
        if (!record.IsValid)
            throw new ValidationException($"Invalid record: {record.Id}");
    })
    .Filter(record => record.RequiresValidation)
    .ToPipe());

// Batch for database insert
pipeline.Add(new PipeBuilder<DataRecord>()
    .Batch(1000, async records => {
        await BulkInsertToDatabase(records);
    })
    .BatchTriggerPeriod(TimeSpan.FromSeconds(10))
    .ToPipe());
```

### Real-time Stream Processing

```csharp
var pipeline = new Pipeline<StreamEvent>();

// Filter stage
var filterPipe = new PipeBuilder<StreamEvent>()
    .Action(_ => { })
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