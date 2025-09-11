namespace Simpipe.Blocks;

/// <summary>
/// A null object pattern implementation that discards all items without processing.
/// </summary>
/// <typeparam name="T">The type of items to discard.</typeparam>
/// <remarks>
/// <para>
/// NullBlock implements <see cref="IActionBlock{T}"/> but performs no actual processing.
/// All items sent to this block are immediately discarded, and all operations complete successfully.
/// </para>
/// <para>
/// This block is useful for:
/// • Terminal pipes that only perform side effects
/// • Testing and mocking scenarios
/// • Conditional processing where some branches should discard items
/// • Default routing destinations
/// </para>
/// <para>
/// NullBlock is a singleton - use the <see cref="Instance"/> property to access the shared instance.
/// </para>
/// </remarks>
/// <example>
/// Using NullBlock for conditional routing:
/// <code>
/// var processingPipe = CreateProcessingPipe();
/// var discardPipe = new Pipe&lt;Message&gt;(
///     new PipeOptions&lt;Message&gt;("discard"),
///     _ => NullBlock&lt;Message&gt;.Instance
/// );
/// 
/// sourcePipe.LinkTo(message => {
///     if (message.Priority == Priority.Low &amp;&amp; SystemLoad() > 0.8)
///         return discardPipe;  // Discard low priority messages under load
///     return processingPipe;
/// });
/// </code>
/// </example>
public class NullBlock<T> : IActionBlock<T>
{
    /// <summary>
    /// Gets the singleton instance of the null block.
    /// </summary>
    /// <value>The shared <see cref="NullBlock{T}"/> instance.</value>
    /// <remarks>
    /// Since NullBlock has no state and performs no processing, a single instance can be safely
    /// shared across the entire application.
    /// </remarks>
    public static NullBlock<T> Instance { get; } = new();

    /// <summary>
    /// Discards the provided item without processing.
    /// </summary>
    /// <param name="item">The item to discard.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// This method immediately returns a completed task without performing any operations on the item.
    /// </remarks>
    public Task Send(BlockItem<T> item) => Task.CompletedTask;

    /// <summary>
    /// Completes the null block (no-op).
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// Since NullBlock performs no processing and maintains no state, completion is a no-op that
    /// immediately returns a completed task.
    /// </remarks>
    public Task Complete() => Task.CompletedTask;
}