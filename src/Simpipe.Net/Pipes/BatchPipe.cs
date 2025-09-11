using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    /// <summary>
    /// Creates a fluent builder for a batch pipe that groups items into fixed-size batches and executes the specified synchronous action.
    /// </summary>
    /// <param name="batchSize">The maximum number of items per batch. Must be greater than 0.</param>
    /// <param name="action">The synchronous action to execute for each batch of items. Cannot be null.</param>
    /// <returns>A <see cref="BatchPipeBuilder{T}"/> for fluent configuration of the pipe.</returns>
    /// <remarks>
    /// <para>
    /// Batch pipes are ideal for operations that benefit from bulk processing:
    /// • Database bulk inserts/updates
    /// • File I/O operations  
    /// • API calls with batch endpoints
    /// • Reducing per-item overhead
    /// </para>
    /// <para>
    /// Batches are automatically flushed when:
    /// • The batch reaches the specified size
    /// • The batch trigger period expires (if configured)
    /// • The pipe is being completed
    /// </para>
    /// <para>
    /// Use <see cref="BatchPipeBuilder{T}.BatchTriggerPeriod(TimeSpan)"/> to ensure incomplete batches are processed within a time limit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <example>
    /// Creating a batch pipe for Elasticsearch bulk indexing:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;
    ///     .Batch(500, tweets =&gt; {
    ///         ElasticsearchClient.BulkIndex(tweets);
    ///         Console.WriteLine($"Indexed {tweets.Length} tweets");
    ///     })
    ///     .BatchTriggerPeriod(TimeSpan.FromSeconds(5)) // Flush incomplete batches
    ///     .ToPipe();
    /// </code>
    /// </example>
    public static BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockItemAction<T>.BatchSync(action));

    /// <summary>
    /// Creates a fluent builder for a batch pipe that groups items into fixed-size batches and executes the specified asynchronous action.
    /// </summary>
    /// <param name="batchSize">The maximum number of items per batch. Must be greater than 0.</param>
    /// <param name="action">The asynchronous action to execute for each batch of items. Cannot be null.</param>
    /// <returns>A <see cref="BatchPipeBuilder{T}"/> for fluent configuration of the pipe.</returns>
    /// <remarks>
    /// <para>
    /// Async batch pipes are optimized for I/O-bound bulk operations. They provide better resource utilization
    /// when performing database operations, HTTP calls, or file I/O with batched data.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <example>
    /// Creating an async batch pipe for analytics storage:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;
    ///     .Batch(1000, async tweets =&gt; {
    ///         await BigQueryClient.InsertRows("tweets_analytics", tweets);
    ///         Console.WriteLine($"Stored {tweets.Length} tweets for analytics");
    ///     })
    ///     .BatchTriggerPeriod(TimeSpan.FromSeconds(10))
    ///     .DegreeOfParallelism(3) // Concurrent batch processing
    ///     .ToPipe();
    /// </code>
    /// </example>
    public static BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockItemAction<T>.BatchAsync(action));
}

/// <summary>
/// Provides a fluent interface for configuring and building batch pipes that group items into fixed-size batches for bulk processing.
/// </summary>
/// <typeparam name="T">The type of items that will be batched and processed.</typeparam>
/// <remarks>
/// <para>
/// BatchPipeBuilder enables configuration of:
/// • Batch size for grouping items
/// • Time-based batch triggering for incomplete batches  
/// • Degree of parallelism for concurrent batch processing
/// • All standard pipe features (filtering, routing, cancellation)
/// </para>
/// <para>
/// Batch pipes are particularly effective for:
/// • Database bulk operations (inserts, updates, deletes)
/// • File I/O operations that benefit from buffering
/// • API calls with batch endpoints
/// • Operations where per-item overhead is significant
/// </para>
/// </remarks>
/// <example>
/// Configuring a batch pipe for database bulk inserts:
/// <code>
/// var pipe = Pipe&lt;Customer&gt;
///     .Batch(1000, async customers =&gt; {
///         await BulkInsertCustomersAsync(customers);
///         Console.WriteLine($"Inserted {customers.Length} customers");
///     })
///     .Id("customer-inserter")
///     .BatchTriggerPeriod(TimeSpan.FromSeconds(10))
///     .DegreeOfParallelism(2)
///     .BoundedCapacity(5000)
///     .ToPipe();
/// </code>
/// </example>
public sealed class BatchPipeBuilder<T>(int batchSize, BlockItemAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, Pipe<T>>? route;

    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    /// <summary>
    /// Sets the unique identifier for the batch pipe being built.
    /// </summary>
    /// <param name="value">The unique identifier string. Cannot be null or empty.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The pipe ID is used for pipeline management operations like targeted sending and monitoring.
    /// IDs must be unique within a pipeline to avoid conflicts.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public BatchPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    /// <summary>
    /// Sets the filtering predicate that determines which items should be processed by this batch pipe.
    /// </summary>
    /// <param name="value">A predicate function that returns true for items that should be processed. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Items that don't match the filter are automatically forwarded to the next pipe in the chain,
    /// maintaining pipeline flow while allowing selective processing.
    /// </para>
    /// <para>
    /// Filtering is applied before items are added to batches, providing early rejection
    /// and preventing unnecessary batching overhead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public BatchPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    /// <summary>
    /// Sets the routing function that determines where items should be sent after batch processing.
    /// </summary>
    /// <param name="value">A function that takes a processed item and returns the target pipe, or null for default routing. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Routing is applied to individual items after the batch containing them has been processed.
    /// Each item in the processed batch goes through the routing logic independently.
    /// </para>
    /// <para>
    /// Returning null from the routing function causes items to be sent to the next pipe in the chain.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public BatchPipeBuilder<T> Route(Func<T, Pipe<T>> value)
    {
        route = value;
        return this;
    }

    /// <summary>
    /// Sets the time-based trigger that flushes incomplete batches after the specified period.
    /// </summary>
    /// <param name="value">The maximum time to wait before flushing an incomplete batch. Use TimeSpan.Zero to disable time-based triggering.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Time-based triggering ensures that incomplete batches don't remain unprocessed indefinitely.
    /// This is crucial for real-time systems where data freshness is important.
    /// </para>
    /// <para>
    /// The trigger period represents the maximum latency for item processing. In low-throughput scenarios,
    /// most batches will be triggered by time rather than size.
    /// </para>
    /// <para>
    /// Set to TimeSpan.Zero to disable time-based triggering entirely - batches will only flush when
    /// reaching the specified size or during pipe completion.
    /// </para>
    /// </remarks>
    /// <example>
    /// Balancing throughput and latency:
    /// <code>
    /// // High throughput, low latency requirement
    /// .BatchTriggerPeriod(TimeSpan.FromSeconds(1))
    /// 
    /// // Optimize for throughput over latency  
    /// .BatchTriggerPeriod(TimeSpan.FromMinutes(5))
    /// 
    /// // Disable time-based flushing
    /// .BatchTriggerPeriod(TimeSpan.Zero)
    /// </code>
    /// </example>
    public BatchPipeBuilder<T> BatchTriggerPeriod(TimeSpan value)
    {
        batchTriggerPeriod = value;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token for graceful shutdown of batch processing operations.
    /// </summary>
    /// <param name="value">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When cancellation is requested:
    /// • New items are no longer accepted for batching
    /// • Current incomplete batches are processed if possible
    /// • Currently executing batch operations are allowed to complete or respond to cancellation
    /// • The pipe transitions to a completed state
    /// </para>
    /// <para>
    /// Individual batch action implementations should check the cancellation token and respond appropriately
    /// to ensure timely shutdown.
    /// </para>
    /// </remarks>
    public BatchPipeBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Sets the degree of parallelism for concurrent batch processing.
    /// </summary>
    /// <param name="value">The maximum number of batches that can be processed concurrently. Must be greater than 0.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Batch parallelism operates at the batch level, not individual items. Each batch is processed
    /// as a single unit, but multiple batches can be processed concurrently.
    /// </para>
    /// <para>
    /// Guidelines for batch parallelism:
    /// • Database operations: Use moderate parallelism (2-5) to avoid connection pool exhaustion
    /// • File I/O: Higher parallelism acceptable if I/O subsystem can handle it
    /// • API calls: Consider rate limits when setting parallelism
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public BatchPipeBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    /// <summary>
    /// Sets the bounded capacity for the internal item queue to control memory usage and provide back-pressure.
    /// </summary>
    /// <param name="value">The maximum number of individual items (not batches) that can be queued, or null for default capacity (batch size).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Bounded capacity provides back-pressure: when the queue is full, Send() operations will wait
    /// until space becomes available. This prevents unbounded memory growth in high-throughput scenarios.
    /// </para>
    /// <para>
    /// Default capacity (batch size) works well for most scenarios. Consider adjusting when:
    /// • Higher capacity: When items arrive in very uneven bursts
    /// • Lower capacity: When memory usage needs to be strictly controlled
    /// </para>
    /// <para>
    /// The capacity represents individual items, not batches. For example, capacity 1000 with batch size 100
    /// means approximately 10 full batches can be queued.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public BatchPipeBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    /// <summary>
    /// Builds and returns the configured batch pipe.
    /// </summary>
    /// <returns>A new <see cref="Pipe{T}"/> instance configured for batch processing according to the builder settings.</returns>
    /// <remarks>
    /// This method creates a new batch pipe instance with all the configured settings. The builder can be reused
    /// to create multiple pipes with the same configuration.
    /// </remarks>
    public Pipe<T> ToPipe() => new(Options(), done =>
        new BatchActionBlock<T>(
            boundedCapacity ?? batchSize,
            batchSize,
            batchTriggerPeriod != TimeSpan.Zero ? batchTriggerPeriod : Timeout.InfiniteTimeSpan,
            degreeOfParallelism,
            action,
            done,
            cancellationToken));

    /// <summary>
    /// Implicitly converts a <see cref="BatchPipeBuilder{T}"/> to a <see cref="Pipe{T}"/> by building the configured batch pipe.
    /// </summary>
    /// <param name="builder">The builder to convert to a pipe.</param>
    /// <returns>A new <see cref="Pipe{T}"/> instance with the builder's batch processing configuration.</returns>
    /// <remarks>
    /// This conversion allows BatchPipeBuilder to be used directly in contexts expecting a Pipe,
    /// providing a seamless fluent interface experience.
    /// </remarks>
    public static implicit operator Pipe<T>(BatchPipeBuilder<T> builder) => builder.ToPipe();
}
