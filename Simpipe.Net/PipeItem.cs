namespace Youscan.Core.Pipes
{
    public readonly record struct PipeItem<T>
    {
        public static readonly PipeItem<T> Empty = new();
        
        readonly IPipeItemHandler handler;

        public bool IsArray { get; }
        public bool IsValue { get; }
        public bool IsEmpty { get; }

        T? Value  { get; }
        T[]? Values { get; }

        public PipeItem()
        {
            Values = default;
            Value = default;
            IsArray = false;
            IsValue = false;
            IsEmpty = true;
            handler = PipeItemEmptyHandler.Instance;
        }

        public PipeItem(T[] values)
        {
            Values = values;
            Value = default;
            IsArray = true;
            IsValue = false;
            IsEmpty = false;
            handler = PipeItemArrayHandler.Instance;
        }

        public PipeItem(T value)
        {
            Value = value;
            Values = null;
            IsArray = false;
            IsValue = true;
            IsEmpty = false;
            handler = PipeItemValueHandler.Instance;
        }

        public async Task Apply(Func<T, Task> receiver) => await handler.Apply(receiver, this);
        public void Apply(Action<T> receiver) => handler.Apply(receiver, this);

        public T GetValue() => handler.GetValue(this);
        public T[] GetArray() => handler.GetArray(this);
        
        public T First() => handler.First(this);
        public PipeItem<T> Where(Func<T, bool> predicate) => handler.Where(predicate, this);

        public int Size => handler.Size(this);

        public static implicit operator T(PipeItem<T> item) => item.GetValue();
        public static implicit operator T[](PipeItem<T> item) => item.GetArray();
        
        interface IPipeItemHandler
        {
            Task Apply(Func<T, Task> receiver, PipeItem<T> item);
            void Apply(Action<T> receiver, PipeItem<T> item);

            T GetValue(PipeItem<T> item);
            T[] GetArray(PipeItem<T> item);

            T First(PipeItem<T> item);
            PipeItem<T> Where(Func<T,bool> predicate, PipeItem<T> item);

            int Size(PipeItem<T> item);
        }

        class PipeItemEmptyHandler : IPipeItemHandler
        {
            public static readonly PipeItemEmptyHandler Instance = new();

            public Task Apply(Func<T, Task> receiver, PipeItem<T> item) => Task.CompletedTask;
            public void Apply(Action<T> receiver, PipeItem<T> item) {}

            public PipeItem<T> Where(Func<T, bool> predicate, PipeItem<T> item) => Empty;

            public T GetValue(PipeItem<T> item) => throw new InvalidCastException("Can't get value from empty");
            public T[] GetArray(PipeItem<T> item) => throw new InvalidCastException("Can't get array from empty");

            public T First(PipeItem<T> item) => throw new InvalidOperationException("Can't get first value from empty");
            public int Size(PipeItem<T> item) => 0;
        }

        class PipeItemValueHandler : IPipeItemHandler
        {
            public static readonly PipeItemValueHandler Instance = new();

            public async Task Apply(Func<T, Task> receiver, PipeItem<T> item) => 
                await receiver(item.Value!);

            public void Apply(Action<T> receiver, PipeItem<T> item) => 
                receiver(item.Value!);

            public PipeItem<T> Where(Func<T, bool> predicate, PipeItem<T> item) => 
                predicate(item) ? item : Empty;
            
            public T GetValue(PipeItem<T> item) => item.Value!;
            public T[] GetArray(PipeItem<T> item) => throw new InvalidCastException("Can't use single value item as array");

            public T First(PipeItem<T> item) => item.Value!;
            public int Size(PipeItem<T> item) => 1;
        }

        class PipeItemArrayHandler : IPipeItemHandler
        {
            public static readonly PipeItemArrayHandler Instance = new();
            
            public async Task Apply(Func<T, Task> receiver, PipeItem<T> item)
            {
                foreach (var value in item.Values!)
                    await receiver(value);
            }

            public void Apply(Action<T> receiver, PipeItem<T> item)
            {
                foreach (var value in item.Values!)
                    receiver(value);
            }
            
            public PipeItem<T> Where(Func<T, bool> predicate, PipeItem<T> item)
            {
                var result = new List<T>();
                
                result.AddRange(item.GetArray().Where(predicate));
                if (result.Count == 0)
                    return Empty;
                
                return result.Count == item.GetArray().Length ? item : new PipeItem<T>(result.ToArray());
            }

            public T GetValue(PipeItem<T> item) => throw new InvalidCastException("Can't use array item as single value");
            public T[] GetArray(PipeItem<T> item) => item.Values!;
            
            public T First(PipeItem<T> item) => item.Values![0];
            public int Size(PipeItem<T> item) => item.Values!.Length;
        }
    }
}