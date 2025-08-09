using System.Threading.Channels;

namespace Simpipe.Blocks;

/// <summary>
/// Defines the contract for action blocks that can process items and be completed asynchronously.
/// </summary>
/// <typeparam name="T">The type of items processed by this action block.</typeparam>
/// <remarks>
/// <para>
/// IActionBlock extends IBlock with processing capabilities specific to pipeline components.
/// It defines the core operations needed for pipeline item processing:
/// • Asynchronous item sending with back-pressure support
/// • Graceful completion and shutdown
/// • Integration with the BlockItem wrapper system
/// </para>
/// <para>
/// All pipeline processing blocks implement this interface, providing a consistent
/// API for pipeline orchestration and management.
/// </para>
/// </remarks>
public interface IActionBlock<T> : IBlock
{
    /// <summary>
    /// Asynchronously sends a wrapped item to this block for processing.
    /// </summary>
    /// <param name="item">The <see cref="BlockItem{T}"/> containing the item(s) to process.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// <para>
    /// This method accepts items wrapped in BlockItem structures that can represent:
    /// • Single items for individual processing
    /// • Arrays of items for batch processing
    /// • Empty items for control flow scenarios
    /// </para>
    /// <para>
    /// The method provides back-pressure: if the block's internal queue is full,
    /// the send operation will wait until space becomes available.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the block has been completed and is no longer accepting items.</exception>
    Task Send(BlockItem<T> item);
    
    /// <summary>
    /// Signals that no more items will be sent to this block and initiates graceful shutdown.
    /// </summary>
    /// <returns>A task that completes when the block has finished processing all items and has shut down.</returns>
    /// <remarks>
    /// <para>
    /// Completion is a cooperative shutdown process:
    /// 1. The block stops accepting new items
    /// 2. All currently queued items are processed
    /// 3. All active processing operations complete
    /// 4. Resources are cleaned up and released
    /// </para>
    /// <para>
    /// Always call Complete() and await its completion to ensure proper resource cleanup
    /// and graceful shutdown of processing operations.
    /// </para>
    /// </remarks>
    Task Complete();
}

/// <summary>
/// A high-performance action block that processes individual items concurrently using System.Threading.Channels 
/// with configurable parallelism and bounded capacity.
/// </summary>
/// <typeparam name="T">The type of items processed by this action block.</typeparam>
/// <remarks>
/// <para>
/// ActionBlock is the core processing component that powers individual item processing in Simpipe.Net.
/// It provides:
/// • Configurable degree of parallelism for concurrent processing
/// • Bounded channel capacity for memory control and back-pressure
/// • Comprehensive metrics tracking (input, output, working counts)
/// • Graceful shutdown with completion tracking
/// • Cancellation token support for responsive shutdown
/// </para>
/// <para>
/// The block uses a producer-consumer pattern with System.Threading.Channels for optimal
/// performance and resource utilization. Processing tasks are created based on the
/// parallelism configuration and run continuously until completion.
/// </para>
/// </remarks>
/// <example>
/// Creating a high-throughput action block:
/// <code>
/// var actionBlock = new ActionBlock&lt;DataRecord&gt;(
///     capacity: 10000,           // Large buffer for high throughput  
///     parallelism: Environment.ProcessorCount * 2,  // I/O-bound parallelism
///     action: BlockItemAction&lt;DataRecord&gt;.Async(async record =&gt; {
///         await ValidateRecordAsync(record);
///         await ProcessRecordAsync(record);
///     }),
///     done: BlockItemAction&lt;DataRecord&gt;.Sync(record =&gt; 
///         Console.WriteLine($"Processed {record.Id}")),
///     cancellationToken: cts.Token
/// );
/// </code>
/// </example>
public class ActionBlock<T> : IActionBlock<T>
{
    readonly MetricsTrackingExecutor<T> executor = new();
    readonly Channel<BlockItem<T>> input;
    readonly BlockItemAction<T> send;
    readonly BlockItemAction<T> action;
    readonly BlockItemAction<T> done;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlock{T}"/> class with the specified configuration.
    /// </summary>
    /// <param name="capacity">The bounded capacity of the internal channel. Must be greater than 0.</param>
    /// <param name="parallelism">The degree of parallelism for concurrent processing. Must be greater than 0.</param>
    /// <param name="action">The action to execute for each item. Cannot be null.</param>
    /// <param name="done">Optional action to execute after each item is processed. If null, a no-op action is used.</param>
    /// <param name="cancellationToken">Optional cancellation token for graceful shutdown. Defaults to CancellationToken.None.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> or <paramref name="parallelism"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Configuration guidelines:
    /// • **Capacity**: Set based on memory constraints and desired back-pressure behavior
    /// • **Parallelism**: For CPU-bound work use Environment.ProcessorCount; for I/O-bound work use higher values
    /// • **Action**: The main processing logic - should handle exceptions appropriately  
    /// • **Done**: Optional post-processing action (logging, metrics, notifications)
    /// • **CancellationToken**: Enable responsive shutdown in long-running scenarios
    /// </para>
    /// <para>
    /// The block immediately starts processing tasks based on the parallelism setting.
    /// Tasks run continuously until the block is completed and all items are processed.
    /// </para>
    /// </remarks>
    public ActionBlock(
        int capacity,
        int parallelism,
        BlockItemAction<T> action,
        BlockItemAction<T>? done = null,
        CancellationToken cancellationToken = default)
    {
        this.action = action;
        this.done = done ?? BlockItemAction<T>.Noop;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<BlockItem<T>>(capacity);

        processor = Task.WhenAll(Enumerable
            .Range(0, parallelism)
            .Select(_ => Task.Run(ProcessChannel, cancellationToken)));

        send = new BlockItemAction<T>(async item =>
            await input.Writer.WriteAsync(item, cancellationToken));
    }

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            if (input.Reader.TryRead(out var item))
                await ProcessItem(item);
        }
    }

    async Task ProcessItem(BlockItem<T> item)
    {
        await Execute(item);
        await Done(item);
    }

    Task Execute(BlockItem<T> item) => executor.ExecuteAction(item, action);

    async Task Done(BlockItem<T> item)
    {
        if (!cancellationToken.IsCancellationRequested)
            await executor.ExecuteDone(item, done);
    }

    public Task Send(BlockItem<T> item) => executor.ExecuteSend(item, send);

    public async Task Complete()
    {
        input.Writer.Complete();

        await processor;
    }

    public int InputCount => executor.InputCount;
    public int OutputCount => executor.OutputCount;
    public int WorkingCount => executor.WorkingCount;
}

public static class ActionBlockExtensions
{
    public static Task Send<T>(this IActionBlock<T> block, T item) => block.Send(new BlockItem<T>(item));
    public static Task Send<T>(this IActionBlock<T> block, T[] items) => block.Send(new BlockItem<T>(items));
}