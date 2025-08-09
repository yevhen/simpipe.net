namespace Simpipe.Blocks;

/// <summary>
/// A decorator block that conditionally filters items before forwarding them to an inner block for processing.
/// </summary>
/// <typeparam name="T">The type of items to filter.</typeparam>
/// <remarks>
/// <para>
/// FilterBlock implements the decorator pattern to add filtering capability to any <see cref="IActionBlock{T}"/>.
/// Items that pass the filter are sent to the inner block for processing, while filtered-out items
/// are sent directly to a done action (typically forwarding to the next pipe).
/// </para>
/// <para>
/// This block is primarily used internally by the Pipe infrastructure to implement the Filter
/// functionality, but can be used directly for custom filtering scenarios.
/// </para>
/// <para>
/// Key characteristics:
/// • Zero processing overhead for filtered items
/// • Maintains metrics from the inner block
/// • Preserves back-pressure from the inner block
/// • Thread-safe filter evaluation
/// </para>
/// </remarks>
/// <example>
/// Creating a filtered processing block:
/// <code>
/// // Create an inner processing block
/// var processor = new ActionBlock&lt;Order&gt;(
///     capacity: 100,
///     parallelism: 4,
///     action: BlockItemAction&lt;Order&gt;.Async(ProcessOrder)
/// );
/// 
/// // Wrap with filtering
/// var filtered = new FilterBlock&lt;Order&gt;(
///     inner: processor,
///     filter: order => order.Status == OrderStatus.Pending,
///     done: BlockItemAction&lt;Order&gt;.Async(async order => {
///         // Handle filtered-out orders
///         await LogSkippedOrder(order);
///     })
/// );
/// 
/// // Only pending orders will be processed
/// await filtered.Send(new BlockItem&lt;Order&gt;(order));
/// </code>
/// </example>
public class FilterBlock<T> : IActionBlock<T>
{
    private readonly IActionBlock<T> inner;
    private readonly Func<T, bool> filter;
    private readonly BlockItemAction<T> done;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterBlock{T}"/> class with the specified filter and inner block.
    /// </summary>
    /// <param name="inner">The inner block that will process items passing the filter. Cannot be null.</param>
    /// <param name="filter">The predicate function that determines which items to process. Cannot be null.</param>
    /// <param name="done">The action to execute for items that don't pass the filter. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when inner, filter, or done is null.</exception>
    /// <remarks>
    /// The filter is evaluated for each item's first value (in case of batches). Items not matching
    /// the filter are immediately sent to the done action without entering the inner block's queue.
    /// </remarks>
    public FilterBlock(IActionBlock<T> inner, Func<T, bool> filter, BlockItemAction<T> done)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
        this.done = done ?? throw new ArgumentNullException(nameof(done));
    }

    /// <summary>
    /// Gets the total number of items received by the filter block.
    /// </summary>
    /// <value>The cumulative input count from the inner block.</value>
    public int InputCount => inner.InputCount;

    /// <summary>
    /// Gets the total number of items that have completed processing.
    /// </summary>
    /// <value>The cumulative output count from the inner block.</value>
    public int OutputCount => inner.OutputCount;

    /// <summary>
    /// Gets the current number of items being processed by the inner block.
    /// </summary>
    /// <value>The working count from the inner block.</value>
    public int WorkingCount => inner.WorkingCount;

    /// <summary>
    /// Sends an item through the filter to either the inner block or done action.
    /// </summary>
    /// <param name="item">The item to filter and potentially process.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// The filter predicate is applied to the item. If it passes, the item is sent to the inner block.
    /// If it fails, the item is sent directly to the done action. This provides efficient bypass for
    /// filtered items without queuing overhead.
    /// </remarks>
    public async Task Send(BlockItem<T> item)
    {
        if (!filter(item.GetValue()))
        {
            await done.Execute(item);
            return;
        }

        await inner.Send(item);
    }

    /// <summary>
    /// Completes the filter block and its inner block.
    /// </summary>
    /// <returns>A task that represents the completion of the inner block.</returns>
    /// <remarks>
    /// Completion is delegated to the inner block, ensuring all queued items that passed the filter
    /// are fully processed before completion.
    /// </remarks>
    public Task Complete() => inner.Complete();
}