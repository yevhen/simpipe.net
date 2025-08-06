namespace Simpipe.Pipes
{
    public interface IPipe<T>
    {
        string Id { get; }
        IPipe<T>? Next { get; set; }
        
        IBlock<T> Block { get; }
        IBlock<T> Target(T item);

        Task Send(T item);
        Task SendNext(T item);

        void Complete();
        Task Completion { get; }
        
        int InputCount { get; }
        int OutputCount { get; }
        int WorkingCount { get; }

        void LinkTo(Func<T, IPipe<T>?> route);
    }

    public static class PipeExtensions
    {
        public static void LinkTo<T>(this IPipe<T> pipe, IPipe<T> next) => pipe.Next = next;
    }

    public class Pipe<T> : IPipe<T>
    {
        public string Id { get; }
        public virtual IPipe<T>? Next { get; set; }

        readonly List<Func<T, IPipe<T>?>> routes = new();
        readonly Func<T, bool>? filter;
        readonly PipeAction<T> action;
        readonly TaskCompletionSource completion = new();

        volatile int workingCount;
        volatile int outputCount;

        internal readonly PipeAction<T> blockAction;

        public Pipe(PipeOptions<T> options, PipeAction<T> action)
        {
            this.action = action;

            Id = options.Id();
            filter = options.Filter();

            var route = options.Route();
            if (route != null)
                routes.Add(route);

            blockAction = PipeAction<T>.For(ExecuteAction);
        }

        public IBlock<T> Block { get; internal set; }

        async Task ExecuteAction(PipeItem<T> item)
        {
            Interlocked.Add(ref workingCount, item.Size);
        
            await action.Execute(item);

            Interlocked.Add(ref workingCount, -item.Size);
        }

        public IBlock<T> Target(T item) => FilterMatches(item)
            ? Block 
            : RouteTarget(item);

        internal async Task RouteItem(T item)
        {
            Interlocked.Increment(ref outputCount);

            await RouteTarget(item).Send(item);

            Interlocked.Decrement(ref outputCount);
        }

        IBlock<T> RouteTarget(T item)
        {
            var target = Route(item) ?? Next;
            return target == null 
                ? NullBlock<T>.Instance
                : target.Target(item);
        }

        IPipe<T>? Route(T item) => routes
            .Select(route => route(item))
            .FirstOrDefault(pipe => pipe != null);

        public async Task Send(T item)
        {
            if (FilterMatches(item))
            {
                await SendThis(item);
                return;
            }

            await SendNext(item);
        }

        bool FilterMatches(T item) => filter == null || filter(item);

        Task SendThis(T item) => BlockSend(item);

        public async Task SendNext(T item)
        {
            if (Next != null)
                await Next.Send(item);
        }

        public int InputCount => Block.InputCount;
        public int OutputCount => outputCount;
        public int WorkingCount => workingCount;

        public void Complete() => BlockComplete();
        public Task Completion => AwaitCompletion();

        async Task AwaitCompletion()
        {
            try
            {
                await BlockCompletion();
            }
            catch (TaskCanceledException) {}
        }

        public virtual void LinkTo(Func<T, IPipe<T>?> route) => routes.Add(route);

        Task BlockSend(T item) => Block.Send(item);

        void BlockComplete()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Block.Complete();
                    completion.TrySetResult();
                }
                catch (Exception e)
                {
                    completion.TrySetException(e);
                }
            });
        }

        Task BlockCompletion() => completion.Task;
    }
}