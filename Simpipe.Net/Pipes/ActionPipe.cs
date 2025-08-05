using Simpipe.Net;

namespace Youscan.Core.Pipes
{
    public class ActionPipe<T> : Pipe<T>
    {
        readonly ActionBlock<T> block;
        readonly int boundedCapacity;
        readonly TaskCompletionSource completion = new();
        volatile int outputCount;

        public ActionPipe(ActionPipeOptions<T> options) : base(options, options.Action())
        {
            boundedCapacity = options.BoundedCapacity() ?? options.DegreeOfParallelism() * 2;

            block = new ActionBlock<T>(boundedCapacity, options.DegreeOfParallelism(), Execute, async item =>
            {
                Interlocked.Increment(ref outputCount);
                await RouteTarget(item).Send(item);
                Interlocked.Decrement(ref outputCount);
            });

            Block = block;
        }

        public override int InputCount => block.InputCount;
        public override int OutputCount => outputCount;
        
        public int AvailableCapacity => Math.Max(0, boundedCapacity - InputCount - WorkingCount - OutputCount);

        async Task<T> Execute(T item)
        {
            await ExecuteAction(item);
            return item;
        }

        async Task ExecuteAction(T item) => await blockAction.Execute(item);
        
        protected override Task BlockSend(T item) => block.Send(item);
        protected override void BlockComplete()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await block.Complete();
                    completion.TrySetResult();
                }
                catch (Exception e)
                {
                    completion.TrySetException(e);
                }
            });
        }

        protected override Task BlockCompletion() => completion.Task;

        public override IBlock<T> Block { get; }
    }
}