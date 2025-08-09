namespace Simpipe.Blocks;

public class TimerBatchBlock<T> : IBlock
{
    readonly BatchBlock<T> batchBlock;
    readonly PeriodicTimer flushTimer;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    volatile bool recentlyBatchedBySize;
    volatile bool timerFlushInProgress;

    public TimerBatchBlock(int capacity, int batchSize, TimeSpan flushInterval, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.cancellationToken = cancellationToken;

        batchBlock = new BatchBlock<T>(capacity, batchSize, BatchDone, cancellationToken);
        flushTimer = new PeriodicTimer(flushInterval);

        processor = Task.Run(ProcessTimer, cancellationToken);
        return;

        async Task BatchDone(T[] batch)
        {
            await done(batch);

            if (!timerFlushInProgress)
                recentlyBatchedBySize = true;
        }
    }

    async Task ProcessTimer()
    {
        while (await flushTimer.WaitForNextTickAsync(cancellationToken))
        {
            if (recentlyBatchedBySize)
            {
                recentlyBatchedBySize = false;
                continue;
            }

            await ForceFlush();
        }
    }

    async Task ForceFlush()
    {
        timerFlushInProgress = true;

        await batchBlock.FlushBuffer();

        timerFlushInProgress = false;
    }

    public async Task Send(T item) => await batchBlock.Send(item);

    public async Task Complete()
    {
        await batchBlock.Complete();

        flushTimer.Dispose();

        await processor;
    }

    public int InputCount => batchBlock.InputCount;
    public int OutputCount => batchBlock.OutputCount;
    public int WorkingCount => batchBlock.WorkingCount;
}