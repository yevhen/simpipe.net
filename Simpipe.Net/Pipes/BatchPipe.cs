using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes
{
    public class BatchPipe<T> : Pipe<T>
    {
        readonly TransformBlock<T, T> inputBlock;
        readonly BatchBlock<T> batchBlock;
        readonly TransformManyBlock<T[], T> transformBlock;
        readonly TimeSpan batchTriggerPeriod;
        readonly CancellationToken cancellation;
        readonly Task? timer;
        readonly CancellationTokenSource timerCancellation = new();
        volatile int inputCount;
        
        DateTimeOffset lastBatchTime;

        public BatchPipe(BatchPipeOptions<T> options) : base(options, options.Action())
        {
            batchTriggerPeriod = options.BatchTriggerPeriod();
            cancellation = options.CancellationToken();
            options.BatchSize();

            inputBlock = CreateInputBlock();
            batchBlock = CreateBatchBlock(options);
            transformBlock = CreateTransformBlock(options);

            var linkOptions = new DataflowLinkOptions {PropagateCompletion = true};
            inputBlock.LinkTo(batchBlock, linkOptions);
            batchBlock.LinkTo(transformBlock, linkOptions);

            if (options.BatchTriggerPeriod() != default)
                timer = CreateTimer(options);

            transformBlock.LinkTo(new RoutingBlock<T>(RouteTarget));
            
            Block = inputBlock;
        }

        TransformBlock<T, T> CreateInputBlock()
        {
            return new TransformBlock<T, T>(x => Accept(x), new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 1,
                EnsureOrdered = false,
                CancellationToken = cancellation
            });
        }

        BatchBlock<T> CreateBatchBlock(BatchPipeOptions<T> options)
        {
            return new BatchBlock<T>(options.BatchSize(), new GroupingDataflowBlockOptions
            {
                BoundedCapacity = options.BoundedCapacity() ?? options.BatchSize(),
                EnsureOrdered = false,
                CancellationToken = cancellation
            });
        }

        TransformManyBlock<T[], T> CreateTransformBlock(BatchPipeOptions<T> options)
        {
            return new TransformManyBlock<T[], T>(async x => await Execute(x), new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = options.DegreeOfParallelism(),
                CancellationToken = cancellation,
                MaxDegreeOfParallelism = options.DegreeOfParallelism()
            });
        }

        public override int InputCount => inputCount;
        public override int OutputCount => transformBlock.OutputCount;

        T Accept(T item)
        {
            Interlocked.Increment(ref inputCount);
            return item;
        }

        async Task<T[]> Execute(T[] item)
        {
            RecordLastBatchTime();

            Interlocked.Add(ref inputCount, -item.Length);
            await ExecuteAction(item);

            return item;
        }

        void RecordLastBatchTime() => lastBatchTime = DateTime.Now;

        async Task? CreateTimer(BatchPipeOptions<T> options)
        {
            var t = new PeriodicTimer(options.BatchTriggerPeriod());
            var cancel = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken(), timerCancellation.Token);
            while (!cancel.IsCancellationRequested && await t.WaitForNextTickAsync(cancel.Token))
                if (ShouldTriggerBatch(DateTime.Now, lastBatchTime, batchTriggerPeriod))
                    TriggerBatch();
        }

        public void TriggerBatch() => batchBlock.TriggerBatch();

        public static bool ShouldTriggerBatch(DateTime now, DateTimeOffset lastBatchTime, TimeSpan triggerPeriod) => 
            now - lastBatchTime >= triggerPeriod;
        
        async Task ExecuteAction(T[] item) => await blockAction.Execute(item);

        protected override async Task BlockSend(T item) => await inputBlock.SendAsync(item, cancellation);

        protected override void BlockComplete()
        {
            CancelTimer();
            CompleteBlock();
        }

        void CancelTimer() => timerCancellation.Cancel();
        void CompleteBlock() => inputBlock.Complete();

        protected override Task BlockCompletion() => Task.WhenAll(TimerCompletion(), ActionBlockCompletion());
        public override ITargetBlock<T> Block { get; }

        async Task TimerCompletion()
        {
            if (timer == null)
                return;
            
            try
            {
                await timer;
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        async Task ActionBlockCompletion() => await transformBlock.Completion;
    }
}