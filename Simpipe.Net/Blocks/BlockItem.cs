namespace Simpipe.Pipes;

public readonly record struct BlockItem<T>
{
    public static readonly BlockItem<T> Empty = new();

    readonly IBlockItemHandler handler;

    public bool IsArray { get; }
    public bool IsValue { get; }
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

    public async Task Apply(Func<T, Task> receiver) => await handler.Apply(receiver, this);
    public void Apply(Action<T> receiver) => handler.Apply(receiver, this);

    public T GetValue() => handler.GetValue(this);
    public T[] GetArray() => handler.GetArray(this);

    public T First() => handler.First(this);
    public BlockItem<T> Where(Func<T, bool> predicate) => handler.Where(predicate, this);

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