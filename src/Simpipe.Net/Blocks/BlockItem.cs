namespace Simpipe.Blocks;

/// <summary>
/// A lightweight wrapper that can hold either a single item or an array of items, providing unified processing interface for pipeline blocks.
/// </summary>
/// <typeparam name="T">The type of items contained in the wrapper.</typeparam>
/// <remarks>
/// <para>
/// BlockItem enables unified handling of both individual items and batches within the same processing pipeline.
/// This design allows blocks to process single items or batches efficiently without type system complexity.
/// </para>
/// <para>
/// The struct provides three distinct states:
/// • **Value**: Contains a single item for individual processing
/// • **Array**: Contains multiple items for batch processing  
/// • **Empty**: Contains no items (used for control flow and completion signaling)
/// </para>
/// <para>
/// BlockItem uses the strategy pattern internally to provide efficient, type-safe operations
/// for each state without boxing or unnecessary allocations.
/// </para>
/// </remarks>
/// <example>
/// Working with BlockItem in different scenarios:
/// <code>
/// // Single item processing
/// var singleItem = new BlockItem&lt;string&gt;("hello");
/// singleItem.Apply(item =&gt; Console.WriteLine(item));
/// 
/// // Batch processing
/// var batchItem = new BlockItem&lt;string&gt;(new[] { "hello", "world" });
/// batchItem.Apply(item =&gt; Console.WriteLine(item)); // Prints both items
/// 
/// // Conditional processing
/// if (blockItem.IsValue)
///     ProcessSingle(blockItem.GetValue());
/// else if (blockItem.IsArray)
///     ProcessBatch(blockItem.GetArray());
/// </code>
/// </example>
public readonly record struct BlockItem<T>
{
    public static readonly BlockItem<T> Empty = new();

    readonly IBlockItemHandler handler;

    /// <summary>
    /// Gets a value indicating whether this instance contains an array of items.
    /// </summary>
    /// <value>true if this instance contains multiple items; otherwise, false.</value>
    public bool IsArray { get; }
    
    /// <summary>
    /// Gets a value indicating whether this instance contains a single item.
    /// </summary>
    /// <value>true if this instance contains exactly one item; otherwise, false.</value>
    public bool IsValue { get; }
    
    /// <summary>
    /// Gets a value indicating whether this instance is empty (contains no items).
    /// </summary>
    /// <value>true if this instance contains no items; otherwise, false.</value>
    public bool IsEmpty { get; }

    T? Value  { get; }
    T[]? Values { get; }

    public BlockItem()
    {
        Values = null;
        Value = default;
        IsArray = false;
        IsValue = false;
        IsEmpty = true;
        handler = BlockItemEmptyHandler.Instance;
    }

    public BlockItem(T[] values)
    {
        Values = values;
        Value = default;
        IsArray = true;
        IsValue = false;
        IsEmpty = false;
        handler = BlockItemArrayHandler.Instance;
    }

    public BlockItem(T value)
    {
        Value = value;
        Values = null;
        IsArray = false;
        IsValue = true;
        IsEmpty = false;
        handler = BlockItemValueHandler.Instance;
    }

    /// <summary>
    /// Applies the specified asynchronous function to all items contained in this instance.
    /// </summary>
    /// <param name="receiver">The asynchronous function to apply to each item. Cannot be null.</param>
    /// <returns>A task that represents the completion of applying the function to all items.</returns>
    /// <remarks>
    /// <para>
    /// This method handles the complexity of applying an operation to different item types:
    /// • For single items: Calls the receiver once with the item
    /// • For arrays: Calls the receiver sequentially for each array element
    /// • For empty instances: Returns immediately without calling the receiver
    /// </para>
    /// <para>
    /// Sequential application ensures proper error handling and resource management,
    /// though it may limit parallelism within the batch.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="receiver"/> is null.</exception>
    public async Task Apply(Func<T, Task> receiver) => await handler.Apply(receiver, this);
    
    /// <summary>
    /// Applies the specified synchronous action to all items contained in this instance.
    /// </summary>
    /// <param name="receiver">The synchronous action to apply to each item. Cannot be null.</param>
    /// <remarks>
    /// This is the synchronous version of <see cref="Apply(Func{T, Task})"/> with the same
    /// application semantics but without async overhead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="receiver"/> is null.</exception>
    public void Apply(Action<T> receiver) => handler.Apply(receiver, this);

    /// <summary>
    /// Gets the single item contained in this instance.
    /// </summary>
    /// <returns>The single item of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown when this instance doesn't contain exactly one item.</exception>
    /// <remarks>
    /// Use <see cref="IsValue"/> to check if the instance contains a single item before calling this method.
    /// </remarks>
    public T GetValue() => handler.GetValue(this);
    
    /// <summary>
    /// Gets the array of items contained in this instance.
    /// </summary>
    /// <returns>An array containing all items of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown when this instance doesn't contain an array of items.</exception>
    /// <remarks>
    /// Use <see cref="IsArray"/> to check if the instance contains an array before calling this method.
    /// </remarks>
    public T[] GetArray() => handler.GetArray(this);

    /// <summary>
    /// Gets the first item from this instance, regardless of whether it contains a single item or an array.
    /// </summary>
    /// <returns>The first item of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this instance is empty.</exception>
    /// <remarks>
    /// This method provides convenient access to the first item without having to determine
    /// whether the instance contains a single item or an array.
    /// </remarks>
    public T First() => handler.First(this);
    /// <summary>
    /// Filters the items in this instance using the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate function used to test each item. Cannot be null.</param>
    /// <returns>A new <see cref="BlockItem{T}"/> containing only the items that match the predicate, or <see cref="Empty"/> if no items match.</returns>
    /// <remarks>
    /// <para>
    /// Filtering behavior:
    /// • For single items: Returns the item if it matches the predicate, otherwise returns Empty
    /// • For arrays: Returns a new array with matching items, or Empty if no items match
    /// • For empty instances: Always returns Empty
    /// </para>
    /// <para>
    /// This method enables efficient filtering within the pipeline without changing the processing model.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    public BlockItem<T> Where(Func<T, bool> predicate) => handler.Where(predicate, this);

    /// <summary>
    /// Gets the number of items contained in this instance.
    /// </summary>
    /// <value>0 for empty instances, 1 for single items, or the array length for batch items.</value>
    /// <remarks>
    /// Use this property to determine processing complexity or to implement size-based logic
    /// without having to check the instance type explicitly.
    /// </remarks>
    public int Size => handler.Size(this);

    public static implicit operator T(BlockItem<T> item) => item.GetValue();
    public static implicit operator T[](BlockItem<T> item) => item.GetArray();

    interface IBlockItemHandler
    {
        Task Apply(Func<T, Task> receiver, BlockItem<T> item);
        void Apply(Action<T> receiver, BlockItem<T> item);

        T GetValue(BlockItem<T> item);
        T[] GetArray(BlockItem<T> item);

        T First(BlockItem<T> item);
        BlockItem<T> Where(Func<T,bool> predicate, BlockItem<T> item);

        int Size(BlockItem<T> item);
    }

    class BlockItemEmptyHandler : IBlockItemHandler
    {
        public static readonly BlockItemEmptyHandler Instance = new();

        public Task Apply(Func<T, Task> receiver, BlockItem<T> item) => Task.CompletedTask;
        public void Apply(Action<T> receiver, BlockItem<T> item) {}

        public BlockItem<T> Where(Func<T, bool> predicate, BlockItem<T> item) => Empty;

        public T GetValue(BlockItem<T> item) => throw new InvalidCastException("Can't get value from empty");
        public T[] GetArray(BlockItem<T> item) => throw new InvalidCastException("Can't get array from empty");

        public T First(BlockItem<T> item) => throw new InvalidOperationException("Can't get first value from empty");
        public int Size(BlockItem<T> item) => 0;
    }

    class BlockItemValueHandler : IBlockItemHandler
    {
        public static readonly BlockItemValueHandler Instance = new();

        public async Task Apply(Func<T, Task> receiver, BlockItem<T> item) =>
            await receiver(item.Value!);

        public void Apply(Action<T> receiver, BlockItem<T> item) =>
            receiver(item.Value!);

        public BlockItem<T> Where(Func<T, bool> predicate, BlockItem<T> item) =>
            predicate(item) ? item : Empty;

        public T GetValue(BlockItem<T> item) => item.Value!;
        public T[] GetArray(BlockItem<T> item) => throw new InvalidCastException("Can't use single value item as array");

        public T First(BlockItem<T> item) => item.Value!;
        public int Size(BlockItem<T> item) => 1;
    }

    class BlockItemArrayHandler : IBlockItemHandler
    {
        public static readonly BlockItemArrayHandler Instance = new();

        public async Task Apply(Func<T, Task> receiver, BlockItem<T> item)
        {
            foreach (var value in item.Values!)
                await receiver(value);
        }

        public void Apply(Action<T> receiver, BlockItem<T> item)
        {
            foreach (var value in item.Values!)
                receiver(value);
        }

        public BlockItem<T> Where(Func<T, bool> predicate, BlockItem<T> item)
        {
            var result = new List<T>();

            result.AddRange(item.GetArray().Where(predicate));
            if (result.Count == 0)
                return Empty;

            return result.Count == item.GetArray().Length ? item : new BlockItem<T>(result.ToArray());
        }

        public T GetValue(BlockItem<T> item) => throw new InvalidCastException("Can't use array item as single value");
        public T[] GetArray(BlockItem<T> item) => item.Values!;

        public T First(BlockItem<T> item) => item.Values![0];
        public int Size(BlockItem<T> item) => item.Values!.Length;
    }
}