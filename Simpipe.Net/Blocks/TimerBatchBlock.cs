namespace Simpipe.Blocks;

/// <summary>
/// Extends batch accumulation with time-based flushing to prevent data staleness in low-throughput scenarios.
/// </summary>
/// <typeparam name="T">The type of items to batch.</typeparam>
/// <remarks>
/// <para>
/// TimerBatchBlock wraps a <see cref="BatchBlock{T}"/> and adds periodic flushing based on time intervals.
/// This ensures that incomplete batches are processed within a maximum latency window, critical for
/// real-time systems and user-facing applications.
/// </para>
/// <para>
/// Flushing behavior:
/// • Batches flush immediately when reaching the configured size
/// • Incomplete batches flush after the specified time interval
/// • Timer resets after each size-based flush to avoid redundant flushes
/// • Final flush occurs during completion regardless of size or time
/// </para>
/// <para>
/// This block is ideal for:
/// • Real-time data pipelines with latency requirements
/// • User-facing systems where responsiveness is critical
/// • Variable-rate data streams that may have quiet periods
/// • Preventing data accumulation during low-activity periods
/// </para>
/// </remarks>
/// <example>
/// Real-time analytics with bounded latency:
/// <code>
/// var batchBlock = new TimerBatchBlock&lt;ClickEvent&gt;(
///     capacity: 5000,
///     batchSize: 100,
///     flushInterval: TimeSpan.FromSeconds(2),  // Max 2-second latency
///     done: async batch => {
///         await AnalyticsService.ProcessEvents(batch);
///         Console.WriteLine($"Processed {batch.Length} events");
///     }
/// );
/// 
/// // Events are batched by size OR time
/// await foreach (var clickEvent in eventStream)
/// {
///     await batchBlock.Send(clickEvent);
/// }
/// 
/// await batchBlock.Complete();
/// </code>
/// </example>
public class TimerBatchBlock<T> : IBlock
{
    readonly BatchBlock<T> batchBlock;
    readonly PeriodicTimer flushTimer;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    volatile bool recentlyBatchedBySize;
    volatile bool timerFlushInProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerBatchBlock{T}"/> class with size and time-based batching.
    /// </summary>
    /// <param name="capacity">The bounded capacity of the input buffer. Must be greater than 0.</param>
    /// <param name="batchSize">The number of items that triggers a batch flush. Must be greater than 0.</param>
    /// <param name="flushInterval">The time interval for flushing incomplete batches. Must be positive.</param>
    /// <param name="done">The action to execute for each completed batch. Cannot be null.</param>
    /// <param name="cancellationToken">Optional cancellation token for graceful shutdown.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity or batchSize is less than or equal to 0, or flushInterval is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when done is null.</exception>
    /// <remarks>
    /// <para>
    /// The timer runs continuously in the background, checking for incomplete batches to flush.
    /// The implementation optimizes to avoid redundant flushes when batches are already being
    /// flushed by size triggers.
    /// </para>
    /// </remarks>
    public TimerBatchBlock(int capacity, int batchSize, TimeSpan flushInterval, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.cancellationToken = cancellationToken;

        batchBlock = new BatchBlock<T>(capacity, batchSize, BatchDone, cancellationToken);
        flushTimer = new PeriodicTimer(flushInterval);

        processor = Task.Run(ProcessTimer, cancellationToken);
        return;

        async Task BatchDone(T[] batch)
        {
            await done(batch);

            if (!timerFlushInProgress)
                recentlyBatchedBySize = true;
        }
    }

    async Task ProcessTimer()
    {
        while (await flushTimer.WaitForNextTickAsync(cancellationToken))
        {
            if (recentlyBatchedBySize)
            {
                recentlyBatchedBySize = false;
                continue;
            }

            await ForceFlush();
        }
    }

    async Task ForceFlush()
    {
        timerFlushInProgress = true;

        await batchBlock.FlushBuffer();

        timerFlushInProgress = false;
    }

    /// <summary>
    /// Sends an item to be accumulated in the current batch.
    /// </summary>
    /// <param name="item">The item to add to the batch.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// Items are buffered and will be flushed either when the batch size is reached or when
    /// the flush interval expires, whichever comes first.
    /// </remarks>
    public async Task Send(T item) => await batchBlock.Send(item);

    /// <summary>
    /// Completes the block, stopping the timer and flushing any remaining items.
    /// </summary>
    /// <returns>A task that represents the completion of all batching operations.</returns>
    /// <remarks>
    /// <para>
    /// Completion process:
    /// 1. The underlying batch block is completed (flushes remaining items)
    /// 2. The flush timer is disposed
    /// 3. The timer processing task is awaited
    /// </para>
    /// </remarks>
    public async Task Complete()
    {
        await batchBlock.Complete();

        flushTimer.Dispose();

        await processor;
    }

    /// <summary>
    /// Gets the total number of items received for batching.
    /// </summary>
    /// <value>The cumulative count from the underlying batch block.</value>
    public int InputCount => batchBlock.InputCount;

    /// <summary>
    /// Gets the total number of items that have been sent in completed batches.
    /// </summary>
    /// <value>The cumulative output count from the underlying batch block.</value>
    public int OutputCount => batchBlock.OutputCount;

    /// <summary>
    /// Gets the current number of items being processed in the done action.
    /// </summary>
    /// <value>The working count from the underlying batch block.</value>
    public int WorkingCount => batchBlock.WorkingCount;
}