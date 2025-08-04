using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes;

public class DynamicBatchPipe<T> : Pipe<T>
{
    readonly BatchPipe<T> block;
    readonly Task? timer;
    readonly CancellationTokenSource timerCancellation = new();
    readonly DynamicBatchIntervalController controller;

    DateTimeOffset lastBatchTime;
    int lastInterval;
    bool flushing;

    public DynamicBatchPipe(DynamicBatchPipeOptions<T> options) : base(options, options.Action())
    {
        block = CreateBatchBlock(options);

        var initialInterval = (int) options.InitialBatchInterval().TotalMilliseconds;
        var maxInterval = (int) options.MaxBatchInterval().TotalMilliseconds;
        
        controller = new DynamicBatchIntervalController(
            initialInterval, roundToMultipleOf: 50, roundUp: false, maxInterval);
        
        lastBatchTime = DateTimeOffset.Now;
        lastInterval = controller.NextBatchInterval();
        
        timer = RunTimer(options);
    }

    public override IPipe<T>? Next
    {
        get => block.Next;
        set => block.Next = value;
    }
    public override void LinkTo(Func<T, IPipe<T>?> route) => block.LinkTo(route);

    public override int InputCount => block.InputCount;
    public override int OutputCount => block.OutputCount;
    public override int WorkingCount => block.WorkingCount;

    BatchPipe<T> CreateBatchBlock(DynamicBatchPipeOptions<T> options) => 
        new BatchPipeOptions<T>(options.MaxBatchSize(), PipeAction<T>.For((T[] items) => Execute(items)))
            .DegreeOfParallelism(options.DegreeOfParallelism())
            .CancellationToken(options.CancellationToken()).ToPipe();

    async Task Execute(T[] items)
    {
        if (items.Length == 0)
            return;

        flushing = true;
        
        var sw = Stopwatch.StartNew();
        await blockAction.Execute(items);
       
        controller.RecordLastBatch(lastInterval, (int)sw.ElapsedMilliseconds);

        if (flushing == false)
            controller.Reset(); // start over if flush was triggered by max batch size
        
        lastInterval = controller.NextBatchInterval();
        lastBatchTime = DateTime.Now;

        flushing = false;
    }

    async Task RunTimer(DynamicBatchPipeOptions<T> options)
    {
        var t = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        var cancel = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken(), timerCancellation.Token);
        while (!cancel.IsCancellationRequested && await t.WaitForNextTickAsync(cancel.Token))
            FlushBatch();
    }

    void FlushBatch()
    {
        if (flushing || !ShouldFlushBatch(DateTime.Now, lastBatchTime, TimeSpan.FromMilliseconds(lastInterval)))
            return;

        block.TriggerBatch();
    }

    public static bool ShouldFlushBatch(DateTime now, DateTimeOffset lastBatchTime, TimeSpan batchInterval) => 
        now - lastBatchTime >= batchInterval;

    protected override async Task BlockSend(T item) => await block.Send(item);

    protected override void BlockComplete()
    {
        CancelTimer();
        CompleteBlock();
    }

    void CancelTimer() => timerCancellation.Cancel();
    void CompleteBlock() => block.Complete();

    protected override Task BlockCompletion() => Task.WhenAll(TimerCompletion(), BatchBlockCompletion());
    public override ITargetBlock<T> Block => block.Block;

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
        
    async Task BatchBlockCompletion() => await block.Completion;
}