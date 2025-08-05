using Simpipe.Net;

namespace Youscan.Core.Pipes
{
    public class BatchPipe<T> : Pipe<T>
    {
        readonly BatchActionBlock<T> block;
        readonly TaskCompletionSource completion = new();
        volatile int outputCount;
        
        public BatchPipe(BatchPipeOptions<T> options) : base(options, options.Action())
        {
            block = new BatchActionBlock<T>(
                options.BoundedCapacity() ?? options.BatchSize(),
                options.BatchSize(),
                options.BatchTriggerPeriod() != TimeSpan.Zero ? options.BatchTriggerPeriod() : Timeout.InfiniteTimeSpan,
                options.DegreeOfParallelism(),
                Execute,
                async items =>
                {
                    Interlocked.Increment(ref outputCount);
                    await RouteTarget(items).Send(items);
                    Interlocked.Decrement(ref outputCount);
                });

            Block = block;
        }

        public override int InputCount => block.InputCount;
        public override int OutputCount => outputCount;

        async Task<T[]> Execute(T[] item)
        {
            await ExecuteAction(item);
            return item;
        }

        async Task ExecuteAction(T[] item) => await blockAction.Execute(item);

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