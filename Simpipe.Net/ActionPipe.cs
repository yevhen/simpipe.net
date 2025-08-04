using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes
{
    public class ActionPipe<T> : Pipe<T>
    {
        readonly TransformBlock<T, T> transform;
        readonly int boundedCapacity;

        public ActionPipe(ActionPipeOptions<T> options) : base(options, options.Action())
        {
            boundedCapacity = options.BoundedCapacity() ?? options.DegreeOfParallelism() * 2;

            transform = new TransformBlock<T, T>(x => Execute(x), new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = false,
                BoundedCapacity = boundedCapacity,
                CancellationToken = options.CancellationToken(),
                MaxDegreeOfParallelism = options.DegreeOfParallelism()
            });
            
            transform.LinkTo(new RoutingBlock<T>(RouteTarget));
            
            Block = transform;
        }

        public override int InputCount => transform.InputCount;
        public override int OutputCount => transform.OutputCount;
        
        public int AvailableCapacity => Math.Max(0, boundedCapacity - InputCount - WorkingCount - OutputCount);

        async Task<T> Execute(T item)
        {
            await ExecuteAction(item);
            return item;
        }

        async Task ExecuteAction(T item) => await blockAction.Execute(item);
        
        protected override Task BlockSend(T item) => transform.SendAsync(item);
        protected override void BlockComplete() => transform.Complete();
        protected override Task BlockCompletion() => transform.Completion;

        public override ITargetBlock<T> Block { get; }
    }
}