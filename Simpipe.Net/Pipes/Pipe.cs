using Simpipe.Net;

namespace Youscan.Core.Pipes
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

    public abstract class Pipe<T> : IPipe<T>
    {
        public string Id { get; }
        public virtual IPipe<T>? Next { get; set; }

        readonly List<Func<T, IPipe<T>?>> routes = new();
        readonly Func<T, bool>? filter;
        readonly PipeAction<T> action;
        volatile int working;

        protected readonly PipeAction<T> blockAction;

        internal Pipe(PipeOptions<T> options, PipeAction<T>? action = null)
        {
            Id = options.Id();

            var route = options.Route();
            if (route != null)
                routes.Add(route);
            
            filter = options.Filter();
            this.action = action ?? PipeAction<T>.None();
            
            blockAction = PipeAction<T>.For(ExecuteAction);
        }

        async Task ExecuteAction(PipeItem<T> item)
        {
            Interlocked.Add(ref working, item.Size);
        
            await action.Execute(item);

            Interlocked.Add(ref working, -item.Size);
        }

        public IBlock<T> Target(T item) => FilterMatches(item)
            ? Block 
            : RouteTarget(item);

        protected IBlock<T> RouteTarget(T item)
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

        public abstract int InputCount { get; }
        public abstract int OutputCount { get; }
        public virtual int WorkingCount => working;

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

        protected abstract Task BlockSend(T item);
        protected abstract void BlockComplete();
        protected abstract Task BlockCompletion();
        public abstract IBlock<T> Block { get; }
    }
}