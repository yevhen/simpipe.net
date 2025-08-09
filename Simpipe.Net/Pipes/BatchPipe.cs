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

    public BatchPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public BatchPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

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

    public BatchPipeBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public BatchPipeBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public BatchPipeBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    public Pipe<T> ToPipe() => new(Options(), done =>
        new BatchActionBlock<T>(
            boundedCapacity ?? batchSize,
            batchSize,
            batchTriggerPeriod != TimeSpan.Zero ? batchTriggerPeriod : Timeout.InfiniteTimeSpan,
            degreeOfParallelism,
            action,
            done,
            cancellationToken));

    public static implicit operator Pipe<T>(BatchPipeBuilder<T> builder) => builder.ToPipe();
}
