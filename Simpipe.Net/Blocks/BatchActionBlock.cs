namespace Simpipe.Blocks;

public class BatchActionBlock<T> : IBlock<T>
{
    readonly TimerBatchBlock<T> batchBlock;
    readonly ActionBlock<T> actionBlock;

    public BatchActionBlock(
        int capacity,
        int batchSize,
        TimeSpan batchFlushInterval,
        int parallelism,
        Func<T[], Task> action,
        Func<T, Task> done,
        CancellationToken cancellationToken = default)
    {
        actionBlock = new ActionBlock<T>(
            capacity: 1,
            parallelism,
            action: batch => action(batch.GetArray()),
            done: async batch => await batch.Apply(done),
            cancellationToken);

        batchBlock = new TimerBatchBlock<T>(
            capacity,
            batchSize,
            batchFlushInterval,
            done: batch => actionBlock.Send(new BlockItem<T>(batch)),
            cancellationToken);
    }

    public int InputCount => batchBlock.InputCount;

    public async Task Send(BlockItem<T> item) => await batchBlock.Send(item);

    public async Task Complete()
    {
        await batchBlock.Complete();
        await actionBlock.Complete();
    }

    public void SetAction(Func<BlockItem<T>, Task> action) => actionBlock.SetAction(action);
    public void SetDone(Func<BlockItem<T>, Task> done) => actionBlock.SetDone(done);
}