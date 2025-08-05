namespace Simpipe.Pipes
{
    public class ActionPipe<T> : Pipe<T>
    {
        readonly ActionBlock<T> block;
        readonly int boundedCapacity;
        readonly TaskCompletionSource completion = new();

        public ActionPipe(ActionPipeOptions<T> options) : base(options, options.Action())
        {
            boundedCapacity = options.BoundedCapacity() ?? options.DegreeOfParallelism() * 2;

            block = new ActionBlock<T>(boundedCapacity, options.DegreeOfParallelism(), Execute, RouteItem);

            Block = block;
        }

        public override int InputCount => block.InputCount;

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