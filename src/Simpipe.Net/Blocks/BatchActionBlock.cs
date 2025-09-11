namespace Simpipe.Blocks;

/// <summary>
/// Combines batch aggregation with concurrent action processing, providing both batching and parallelism in a single component.
/// </summary>
/// <typeparam name="T">The type of items to batch and process.</typeparam>
/// <remarks>
/// <para>
/// BatchActionBlock is a composite block that chains a <see cref="TimerBatchBlock{T}"/> with an <see cref="ActionBlock{T}"/>.
/// This design provides:
/// • Automatic batching by size with configurable batch size
/// • Time-based batch flushing to prevent stale data
/// • Concurrent processing of completed batches with configurable parallelism
/// • Unified metrics tracking across both stages
/// </para>
/// <para>
/// This block is ideal for scenarios requiring both batching and parallel processing:
/// • Bulk database operations with concurrent execution
/// • API calls that benefit from both batching and parallelism
/// • Stream processing with batch aggregation and parallel transformation
/// </para>
/// <para><strong>Performance Characteristics:</strong></para>
/// <list type="bullet">
/// <item><strong>Throughput</strong>: Limited by the slower of batching or action processing</item>
/// <item><strong>Memory</strong>: Bounded by capacity plus batch size times parallelism</item>
/// <item><strong>Latency</strong>: Batch size and flush interval affect processing delay</item>
/// </list>
/// </remarks>
/// <example>
/// Processing streaming data with batched database inserts:
/// <code>
/// var batchProcessor = new BatchActionBlock&lt;SensorReading&gt;(
///     capacity: 10000,           // Input buffer size
///     batchSize: 500,            // Batch size for database
///     batchFlushInterval: TimeSpan.FromSeconds(5),  // Max latency
///     parallelism: 3,            // Concurrent batch processors
///     action: BlockItemAction&lt;SensorReading&gt;.BatchAsync(async batch => {
///         await DatabaseClient.BulkInsert(batch);
///         Console.WriteLine($"Inserted {batch.Length} readings");
///     })
/// );
/// 
/// // Stream sensor data
/// await foreach (var reading in sensorStream)
///     await batchProcessor.Send(new BlockItem&lt;SensorReading&gt;(reading));
/// 
/// await batchProcessor.Complete();
/// </code>
/// </example>
public class BatchActionBlock<T> : IActionBlock<T>
{
    readonly TimerBatchBlock<T> batchBlock;
    readonly ActionBlock<T> actionBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchActionBlock{T}"/> class with the specified batching and processing configuration.
    /// </summary>
    /// <param name="capacity">The bounded capacity of the input buffer. Must be greater than 0.</param>
    /// <param name="batchSize">The number of items to accumulate before processing a batch. Must be greater than 0.</param>
    /// <param name="batchFlushInterval">The maximum time to wait before flushing an incomplete batch.</param>
    /// <param name="parallelism">The degree of parallelism for processing completed batches. Must be greater than 0.</param>
    /// <param name="action">The action to execute for each batch. Cannot be null.</param>
    /// <param name="done">Optional action to execute after each batch is processed.</param>
    /// <param name="cancellationToken">Optional cancellation token for graceful shutdown.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity, batchSize, or parallelism is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <remarks>
    /// The block internally creates a pipeline of TimerBatchBlock → ActionBlock, where batches flow from
    /// the aggregation stage to the processing stage. The capacity parameter controls the input buffer,
    /// while parallelism controls how many batches can be processed concurrently.
    /// </remarks>
    public BatchActionBlock(
        int capacity,
        int batchSize,
        TimeSpan batchFlushInterval,
        int parallelism,
        BlockItemAction<T> action,
        BlockItemAction<T>? done = null,
        CancellationToken cancellationToken = default)
    {
        actionBlock = new ActionBlock<T>(
            capacity: 1,
            parallelism,
            action,
            done,
            cancellationToken);

        batchBlock = new TimerBatchBlock<T>(
            capacity,
            batchSize,
            batchFlushInterval,
            done: actionBlock.Send,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously sends an item to be batched and processed.
    /// </summary>
    /// <param name="item">The item wrapped in a <see cref="BlockItem{T}"/>.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// Items are first accumulated in the batching stage. When a batch is complete (by size or time),
    /// it's forwarded to the action processing stage.
    /// </remarks>
    public async Task Send(BlockItem<T> item) => await batchBlock.Send(item);

    /// <summary>
    /// Completes the block, flushing any remaining items and waiting for all processing to finish.
    /// </summary>
    /// <returns>A task that represents the completion of all batching and processing operations.</returns>
    /// <remarks>
    /// Completion flushes any incomplete batch and ensures all batches are fully processed before returning.
    /// </remarks>
    public async Task Complete()
    {
        await batchBlock.Complete();
        await actionBlock.Complete();
    }

    /// <summary>
    /// Gets the total number of items received for batching.
    /// </summary>
    /// <value>The cumulative count of items sent to the batching stage.</value>
    public int InputCount => batchBlock.InputCount;
    /// <summary>
    /// Gets the total number of items that have been fully processed.
    /// </summary>
    /// <value>The cumulative count of items that completed action processing.</value>
    public int OutputCount => actionBlock.OutputCount;
    /// <summary>
    /// Gets the current number of items being actively processed in actions.
    /// </summary>
    /// <value>The number of items currently being processed by the action stage.</value>
    public int WorkingCount => actionBlock.WorkingCount;
}