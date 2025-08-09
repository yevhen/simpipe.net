namespace Simpipe.Blocks;

/// <summary>
/// Encapsulates actions that can operate on both single items and batches, providing a unified execution interface.
/// </summary>
/// <typeparam name="T">The type of items the action operates on.</typeparam>
/// <remarks>
/// <para>
/// BlockItemAction wraps different types of processing functions to work with the <see cref="BlockItem{T}"/>
/// abstraction. This allows blocks to handle both individual items and batches uniformly.
/// </para>
/// <para>
/// The class provides static factory methods for common action patterns:
/// • <see cref="Sync"/> - Synchronous single-item processing
/// • <see cref="Async"/> - Asynchronous single-item processing
/// • <see cref="BatchSync"/> - Synchronous batch processing
/// • <see cref="BatchAsync"/> - Asynchronous batch processing
/// </para>
/// <para>
/// This abstraction is fundamental to the Simpipe architecture, enabling blocks to compose
/// and chain operations regardless of whether they process items individually or in batches.
/// </para>
/// </remarks>
/// <example>
/// Creating different types of actions:
/// <code>
/// // Single item processing
/// var validateAction = BlockItemAction&lt;Order&gt;.Async(async order => {
///     if (!await ValidateOrder(order))
///         throw new ValidationException($"Invalid order: {order.Id}");
/// });
/// 
/// // Batch processing
/// var bulkSaveAction = BlockItemAction&lt;Order&gt;.BatchAsync(async orders => {
///     await Database.BulkInsert(orders);
///     Console.WriteLine($"Saved {orders.Length} orders");
/// });
/// 
/// // Using in a block
/// var actionBlock = new ActionBlock&lt;Order&gt;(
///     capacity: 100,
///     parallelism: 4,
///     action: validateAction
/// );
/// </code>
/// </example>
public class BlockItemAction<T>(Func<BlockItem<T>, Task> action)
{
    /// <summary>
    /// Gets a no-operation action that immediately completes without processing.
    /// </summary>
    /// <value>A <see cref="BlockItemAction{T}"/> that does nothing.</value>
    /// <remarks>
    /// Useful as a default value or placeholder when an action is required but no processing is needed.
    /// </remarks>
    public static readonly BlockItemAction<T> Noop = new(_ => Task.CompletedTask);
    
    /// <summary>
    /// Creates an action that processes single items synchronously.
    /// </summary>
    /// <param name="action">The synchronous action to execute for each item. Cannot be null.</param>
    /// <returns>A <see cref="BlockItemAction{T}"/> that executes the specified action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <remarks>
    /// The created action extracts single values from BlockItem and executes the provided action synchronously.
    /// </remarks>
    public static BlockItemAction<T> Sync(Action<T> action) => new(item =>
    {
        action(item.GetValue());
        return Task.CompletedTask;
    });

    /// <summary>
    /// Creates an action that processes single items asynchronously.
    /// </summary>
    /// <param name="action">The asynchronous action to execute for each item. Cannot be null.</param>
    /// <returns>A <see cref="BlockItemAction{T}"/> that executes the specified async action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <remarks>
    /// The created action extracts single values from BlockItem and executes the provided async action.
    /// </remarks>
    public static BlockItemAction<T> Async(Func<T, Task> action) => new(item =>
        action(item.GetValue()));

    /// <summary>
    /// Creates an action that processes batches of items synchronously.
    /// </summary>
    /// <param name="action">The synchronous action to execute for each batch. Cannot be null.</param>
    /// <returns>A <see cref="BlockItemAction{T}"/> that executes the specified batch action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <remarks>
    /// The created action extracts arrays from BlockItem and executes the provided action synchronously.
    /// </remarks>
    public static BlockItemAction<T> BatchSync(Action<T[]> action) => new(item =>
    {
        action(item.GetArray());
        return Task.CompletedTask;
    });

    /// <summary>
    /// Creates an action that processes batches of items asynchronously.
    /// </summary>
    /// <param name="action">The asynchronous action to execute for each batch. Cannot be null.</param>
    /// <returns>A <see cref="BlockItemAction{T}"/> that executes the specified async batch action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
    /// <remarks>
    /// The created action extracts arrays from BlockItem and executes the provided async action.
    /// </remarks>
    public static BlockItemAction<T> BatchAsync(Func<T[], Task> action) => new(item =>
        action(item.GetArray()));

    /// <summary>
    /// Executes the encapsulated action on the provided item or batch.
    /// </summary>
    /// <param name="item">The <see cref="BlockItem{T}"/> containing the item(s) to process.</param>
    /// <returns>A task that represents the asynchronous execution.</returns>
    /// <remarks>
    /// This method invokes the underlying action function that was provided during construction.
    /// The action determines how to handle the BlockItem based on its type (single, array, or empty).
    /// </remarks>
    public async Task Execute(BlockItem<T> item) => await action(item);
}