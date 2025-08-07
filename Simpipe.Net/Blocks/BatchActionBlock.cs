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
        BlockItemAction<T> action,
        BlockItemAction<T> done,
        CancellationToken cancellationToken = default)
    {
        actionBlock = new ActionBlock<T>(
            capacity: 1,
            parallelism,
            action,
            done,
            cancellationToken);

        batchBlock = new TimerBatchBlock<T>(
            capacity,
            batchSize,
            batchFlushInterval,
            done: actionBlock.Send,
            cancellationToken);
    }

    public async Task Send(BlockItem<T> item) => await batchBlock.Send(item);

    public async Task Complete()
    {
        await batchBlock.Complete();
        await actionBlock.Complete();
    }
}