using System.Diagnostics;

namespace Simpipe.Blocks;

/// <summary>
/// Implements fork-join parallelism by distributing items to multiple child blocks and coordinating their completion.
/// </summary>
/// <typeparam name="T">The type of items to process in parallel.</typeparam>
/// <remarks>
/// <para>
/// ParallelBlock is the core implementation behind fork pipes. It sends each incoming item to all
/// registered child blocks simultaneously and tracks when all blocks have completed processing the item.
/// </para>
/// <para>
/// Key characteristics:
/// • Each item is sent to ALL child blocks (broadcast pattern)
/// • Completion tracking ensures all blocks finish before reporting done
/// • Child blocks can have different processing logic and parallelism
/// • Reference-based tracking requires items to be reference types or properly implement equality
/// </para>
/// <para>
/// This block is typically created through the <see cref="Pipe{T}.Fork"/> method rather than directly.
/// </para>
/// <para><strong>Performance Considerations:</strong></para>
/// <list type="bullet">
/// <item>Items must be reference types or value types with proper equality implementation</item>
/// <item>Completion tracking uses a dictionary, so item hash codes should be well-distributed</item>
/// <item>All child blocks receive the same instance, so items should be thread-safe if mutated</item>
/// </list>
/// </remarks>
/// <example>
/// Creating a parallel processing block:
/// <code>
/// var parallel = new ParallelBlock&lt;Document&gt;(
///     blockCount: 3,
///     done: BlockItemAction&lt;Document&gt;.Async(async doc => {
///         Console.WriteLine($"All processing complete for {doc.Id}");
///         await SaveDocument(doc);
///     }),
///     blocksFactory: innerDone => new Dictionary&lt;string, IActionBlock&lt;Document&gt;&gt; {
///         ["validate"] = new ActionBlock&lt;Document&gt;(1, 1, 
///             BlockItemAction&lt;Document&gt;.Async(ValidateDocument), innerDone),
///         ["enrich"] = new ActionBlock&lt;Document&gt;(1, 2,
///             BlockItemAction&lt;Document&gt;.Async(EnrichDocument), innerDone),
///         ["index"] = new ActionBlock&lt;Document&gt;(1, 1,
///             BlockItemAction&lt;Document&gt;.Async(IndexDocument), innerDone)
///     }
/// );
/// 
/// await parallel.Send(new BlockItem&lt;Document&gt;(document));
/// await parallel.Complete();
/// </code>
/// </example>
public class ParallelBlock<T> : IActionBlock<T>
{
    readonly ActionBlock<T> input;
    readonly Dictionary<string, IActionBlock<T>> blocks;
    readonly CompletionTracker<T> completion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelBlock{T}"/> class with the specified configuration.
    /// </summary>
    /// <param name="blockCount">The number of parallel child blocks. Must match the number of blocks returned by blocksFactory.</param>
    /// <param name="done">The action to execute after all child blocks complete processing an item. Cannot be null.</param>
    /// <param name="blocksFactory">A factory function that creates the child blocks with proper done tracking. Cannot be null.</param>
    /// <param name="cancellationToken">Optional cancellation token for graceful shutdown.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when blockCount is less than 1.</exception>
    /// <exception cref="ArgumentNullException">Thrown when done or blocksFactory is null.</exception>
    /// <exception cref="ArgumentException">Thrown when blocksFactory returns a different number of blocks than blockCount.</exception>
    /// <remarks>
    /// <para>
    /// The blocksFactory receives a tracking action that must be passed as the done parameter to each
    /// child block. This enables the ParallelBlock to track when all blocks have completed processing an item.
    /// </para>
    /// <para>
    /// The internal implementation uses a single-threaded ActionBlock to ensure thread-safe distribution
    /// of items to child blocks.
    /// </para>
    /// </remarks>
    public ParallelBlock(
        int blockCount,
        BlockItemAction<T> done,
        Func<BlockItemAction<T>, Dictionary<string, IActionBlock<T>>> blocksFactory,
        CancellationToken cancellationToken = default)
    {
        if (blockCount < 1)
            throw new ArgumentOutOfRangeException(nameof(blockCount), "Block count must be greater than 0.");
        
        completion = new CompletionTracker<T>(blockCount, done ?? throw new ArgumentNullException(nameof(done)));

        blocks = blocksFactory?.Invoke(new BlockItemAction<T>(completion.TrackDone)) 
                 ?? throw new ArgumentNullException(nameof(blocksFactory));

        if (blocks.Count != blockCount)
            throw new ArgumentException("The number of blocks returned by the factory must match the block count.", nameof(blocksFactory));

        input = new ActionBlock<T>(
            capacity: 1,
            parallelism: 1,
            BlockItemAction<T>.Async(SendAll),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the collection of child blocks with their identifiers.
    /// </summary>
    /// <value>An enumerable of key-value pairs where keys are block identifiers and values are the block instances.</value>
    /// <remarks>
    /// This property provides access to the child blocks for monitoring or direct interaction.
    /// The keys are typically used for identification in logging or debugging scenarios.
    /// </remarks>
    public IEnumerable<KeyValuePair<string, IActionBlock<T>>> Blocks => blocks;

    /// <summary>
    /// Sends an item to all child blocks for parallel processing.
    /// </summary>
    /// <param name="item">The item to process in parallel.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// The item is broadcast to all child blocks simultaneously using Task.WhenAll. The method
    /// returns once the item has been queued in all child blocks, not when processing is complete.
    /// </remarks>
    public async Task Send(BlockItem<T> item) => await input.Send(item);

    /// <summary>
    /// Completes the parallel block and all child blocks, waiting for all processing to finish.
    /// </summary>
    /// <returns>A task that represents the completion of all parallel processing.</returns>
    /// <remarks>
    /// <para>
    /// Completion process:
    /// 1. The input distribution block is completed
    /// 2. All child blocks are completed
    /// 3. The completion tracker is completed
    /// 4. The method returns after all items have been fully processed
    /// </para>
    /// </remarks>
    public async Task Complete()
    {
        await input.Complete();

        await CompleteAll();

        await completion.Complete();
    }

    Task SendAll(T item) => Task.WhenAll(blocks.Values.Select(block => block.Send(new BlockItem<T>(item))));
    Task CompleteAll() => Task.WhenAll(blocks.Values.Select(block => block.Complete()));
}

internal class CompletionTracker<T>
{
    readonly ActionBlock<T> completion;
    readonly Dictionary<object, int> completed = new();
    readonly int blockCount;
    readonly BlockItemAction<T> done;

    public CompletionTracker(int blockCount, BlockItemAction<T> done)
    {
        this.blockCount = blockCount;
        this.done = done;

        completion = new ActionBlock<T>(capacity: 1, parallelism: 1, BlockItemAction<T>.Async(ReportDone));
    }

    async Task ReportDone(T item)
    {
        Debug.Assert(item != null, nameof(item) + " != null");

        if (completed.TryGetValue(item, out var currentCount))
            completed[item] = currentCount + 1;
        else
            completed[item] = 1;

        if (completed[item] == blockCount)
            await done.Execute(new BlockItem<T>(item));
    }

    public async Task TrackDone(BlockItem<T> item)
    {
        await completion.Send(item);
    }

    public Task Complete() => completion.Complete();
}