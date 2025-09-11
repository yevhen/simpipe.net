using System.Threading.Channels;

namespace Simpipe.Blocks;

/// <summary>
/// Accumulates items into fixed-size batches for bulk processing operations.
/// </summary>
/// <typeparam name="T">The type of items to batch.</typeparam>
/// <remarks>
/// <para>
/// BatchBlock provides core batching functionality without time-based triggers or parallel processing.
/// It accumulates items until reaching the specified batch size, then executes the batch action.
/// </para>
/// <para>
/// Key characteristics:
/// • Simple size-based batching with no time triggers
/// • Sequential batch processing (no parallelism)
/// • Manual flush capability for incomplete batches
/// • Efficient for predictable, high-throughput scenarios
/// </para>
/// <para>
/// Use BatchBlock when you need simple batching without time constraints. For time-based flushing,
/// use <see cref="TimerBatchBlock{T}"/>. For parallel batch processing, use <see cref="BatchActionBlock{T}"/>.
/// </para>
/// </remarks>
/// <example>
/// Simple batch accumulation for file writing:
/// <code>
/// var batchBlock = new BatchBlock&lt;LogEntry&gt;(
///     capacity: 1000,
///     batchSize: 100,
///     done: async batch => {
///         await File.AppendAllLinesAsync("log.txt", 
///             batch.Select(e => e.ToString()));
///     }
/// );
/// 
/// foreach (var entry in logEntries)
///     await batchBlock.Send(entry);
/// 
/// await batchBlock.Complete(); // Flushes final batch
/// </code>
/// </example>
public class BatchBlock<T> : IBlock
{
    readonly MetricsTrackingExecutor<T> executor = new();
    readonly Channel<T> input;
    readonly LinkedList<T> buffer = [];
    readonly int batchSize;
    readonly BlockItemAction<T> send;
    readonly BlockItemAction<T> flush;
    readonly BlockItemAction<T> done;
    readonly Task processor;
    readonly CancellationToken cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchBlock{T}"/> class with the specified configuration.
    /// </summary>
    /// <param name="capacity">The bounded capacity of the input channel. Must be greater than 0.</param>
    /// <param name="batchSize">The number of items to accumulate before processing. Must be greater than 0.</param>
    /// <param name="done">The action to execute for each completed batch. Cannot be null.</param>
    /// <param name="cancellationToken">Optional cancellation token for graceful shutdown.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity or batchSize is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when done is null.</exception>
    /// <remarks>
    /// The block uses an internal buffer to accumulate items. When the buffer reaches batchSize,
    /// the done action is invoked with the accumulated items as an array.
    /// </remarks>
    public BatchBlock(int capacity, int batchSize, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.batchSize = batchSize;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<T>(capacity);
        processor = Task.Run(ProcessChannel, cancellationToken);

        this.send = new BlockItemAction<T>(async item => await input.Writer.WriteAsync(item, cancellationToken));
        this.flush = new BlockItemAction<T>(async item => await FlushBySize(item));
        this.done = new BlockItemAction<T>(async items => await done(items));
    }

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            while (input.Reader.TryRead(out var item))
                await executor.ExecuteAction(new BlockItem<T>(item), flush);
        }
    }

    async Task FlushBySize(T item)
    {
        buffer.AddLast(item);

        if (buffer.Count < batchSize)
            return;

        await FlushBuffer();
    }

    /// <summary>
    /// Manually flushes the current buffer as a batch, regardless of size.
    /// </summary>
    /// <returns>A task that represents the flush operation.</returns>
    /// <remarks>
    /// <para>
    /// This method forces the current buffer contents to be processed as a batch, even if
    /// the batch size hasn't been reached. Useful for ensuring all data is processed during
    /// shutdown or quiet periods.
    /// </para>
    /// <para>
    /// If the buffer is empty, this method returns immediately without invoking the done action.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Periodic manual flush pattern
    /// var flushTimer = new Timer(_ => batchBlock.FlushBuffer().Wait(), 
    ///     null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    /// </code>
    /// </example>
    public async Task FlushBuffer()
    {
        if (buffer.Count > 0)
            await Done(buffer.ToArray());
        
        buffer.Clear();
    }

    Task Done(T[] batch) => executor.ExecuteDone(new BlockItem<T>(batch), done);

    /// <summary>
    /// Sends an item to be accumulated in the current batch.
    /// </summary>
    /// <param name="item">The item to add to the batch.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// Items are buffered until the batch size is reached. The method provides back-pressure
    /// when the input channel is full.
    /// </remarks>
    public Task Send(T item) => executor.ExecuteSend(new BlockItem<T>(item), send);

    /// <summary>
    /// Completes the block, flushing any remaining items as a final batch.
    /// </summary>
    /// <returns>A task that represents the completion of all batching operations.</returns>
    /// <remarks>
    /// Completion automatically flushes any items remaining in the buffer as a final batch,
    /// ensuring no data is lost during shutdown.
    /// </remarks>
    public async Task Complete()
    {
        input.Writer.Complete();

        await processor;

        await FlushBuffer();
    }

    /// <summary>
    /// Gets the number of items received by the block.
    /// </summary>
    public int InputCount => executor.InputCount;
    
    /// <summary>
    /// Gets the number of batches that have been processed by the block.
    /// </summary>
    public int OutputCount => executor.OutputCount;
    
    /// <summary>
    /// Gets the number of items currently being processed by the block.
    /// </summary>
    public int WorkingCount => executor.WorkingCount;
}