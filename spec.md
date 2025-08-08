# Concurrent Enrichment Pipeline Specification

## Overview

This specification describes a concurrent enrichment pattern for Simpipe.Net that allows processing the same item through multiple independent transformations simultaneously. The design maintains consistency with existing pipeline architecture while providing a clean API for parallel enrichments.

## Core Design Principles

1. **Request/Response Pattern**: Each enrichment follows a request/response interface pattern with separate extraction, execution, and application phases
2. **Fail-Fast Error Handling**: If any enrichment fails, all others are cancelled and the item fails
3. **Reuse Existing Components**: Leverage existing ActionBlock and BatchActionBlock for execution
4. **Single Pipe Class**: No new pipe types - enrichment is a configuration of the existing Pipe class
5. **Mutation Only**: Enrichments modify items in-place rather than transforming types

## API Design

### Basic Usage

```csharp
var enrichmentPipe = Pipe<Order>
    .Parallel(e => e
        .Action<SentimentRequest, SentimentResponse>()
            .Extract(order => new SentimentRequest(order.CustomerFeedback))
            .Execute(async req => await sentimentApi.Analyze(req))
            .Apply((order, resp) => order.SentimentScore = resp.Score)
            .When(order => !string.IsNullOrEmpty(order.CustomerFeedback))
            .DegreeOfParallelism(5)
            
        .Batch<ClassifyRequest, ClassifyResponse>()
            .ExtractBatch(orders => ClassifyRequest.From(orders))
            .Execute(async req => await classifyApi.BatchClassify(req))
            .ApplyBatch((orders, resp) => resp.ApplyTo(orders))
            .BatchSize(100)
            .BatchTimeout(TimeSpan.FromSeconds(5))
            .When(order => order.NeedsClassification)
            .DegreeOfParallelism(3))
    .Id("enrichment")
    .ToPipe();
```

## Enrichment Flow

### Execution Phases

1. **Extract Phase**: Convert items to requests (can be single or batch)
2. **Execute Phase**: Run requests concurrently against external services  
3. **Apply Phase**: Apply responses back to items serially to avoid concurrent mutations

### Flow Characteristics

- **Blocking**: Items wait in the enrichment pipe until all enrichments complete
- **Parallel Execution**: All enrichments for an item run concurrently
- **Serial Application**: Results are collected and applied serially
- **Fail-Fast**: First exception cancels all pending enrichments

## Configuration Options

### Action Enrichment (Single Item)

```csharp
.Action<TRequest, TResponse>()
    .Extract(Func<TItem, TRequest>)                    // Required
    .Execute(Func<TRequest, Task<TResponse>>)          // Required
    .Apply(Action<TItem, TResponse>)                   // Required
    .When(Func<TItem, bool>)                          // Optional filter
    .DegreeOfParallelism(int)                         // Optional concurrency limit
    .CancellationToken(CancellationToken)             // Optional override
```

### Batch Enrichment

```csharp
.Batch<TRequest, TResponse>()
    .ExtractBatch(Func<TItem[], TRequest>)            // Required
    .Execute(Func<TRequest, Task<TResponse>>)         // Required  
    .ApplyBatch(Action<TItem[], TResponse>)           // Required
    .BatchSize(int)                                    // Required
    .BatchTimeout(TimeSpan)                            // Optional timeout
    .When(Func<TItem, bool>)                          // Optional filter
    .DegreeOfParallelism(int)                         // Optional concurrency limit
    .CancellationToken(CancellationToken)             // Optional override
```

## Metrics and Monitoring

### Counter Strategy

1. **Pipe-Level Counter**: Overall enrichment pipe uses standard ItemCounter interface
2. **Per-Enrichment Counters**: Individual enrichments expose their own ItemCounter
3. **Access Pattern**:
   ```csharp
   // Overall pipe metrics
   var pipeCounter = enrichmentPipe.ItemCounter;
   
   // Per-enrichment metrics
   var sentimentCounter = enrichmentPipe.GetEnrichmentCounter("sentiment");
   ```

## Error Handling

### Fail-Fast Implementation

- First exception encountered is thrown immediately
- All other in-flight enrichments are cancelled
- No partial results are applied
- Original exception bubbles up as-is

### Cancellation Flow

- Pipe cancellation token is used by default
- Per-enrichment tokens can override default
- Cancellation triggers fail-fast behavior

## Implementation Notes

### Request/Response Design

The request/response objects handle mapping logic internally:

```csharp
// Single item
public record SentimentRequest(string Text);
public record SentimentResponse(double Score);

// Batch with internal mapping
public record ClassifyRequest
{
    public Dictionary<int, string> Items { get; init; }
    
    public static ClassifyRequest From(Order[] orders) =>
        new() { Items = orders.ToDictionary(o => o.Id, o => o.Description) };
}

public record ClassifyResponse  
{
    public Dictionary<int, string> Classifications { get; init; }
    
    public void ApplyTo(Order[] orders)
    {
        foreach (var order in orders)
        {
            if (Classifications.TryGetValue(order.Id, out var category))
                order.Category = category;
        }
    }
}
```

### Integration with PipeBuilder

The Parallel method on PipeBuilder returns a configured action that can be used with the existing Action method internally, maintaining the single Pipe class design.

## Benefits

1. **Clean Separation**: Request/response pattern prevents concurrent mutation issues
2. **Service Reuse**: Each enrichment can use existing service clients optimally
3. **Flexible Batching**: Mix single and batch enrichments in same pipe
4. **Consistent API**: Follows existing Simpipe.Net patterns
5. **Performance**: True parallel execution with configurable concurrency
6. **Maintainable**: Clear phases make testing and debugging straightforward

## Example Use Cases

1. **Order Processing**: Enrich with sentiment analysis, fraud scoring, and classification
2. **Data Pipeline**: Parallel geocoding, currency conversion, and validation
3. **Document Processing**: Concurrent translation, summarization, and entity extraction
4. **Stream Processing**: Real-time enrichment with multiple ML models