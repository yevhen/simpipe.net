namespace Simpipe;

public class BatchActionBlock<T> : IBlock<T>
{
    readonly BatchBlock<T> batchBlock;
    readonly ActionBlock<T[]> actionBlock;

    public BatchActionBlock(
        int capacity,
        int batchSize,
        TimeSpan batchFlushInterval,
        int parallelism,
        Func<T[], Task> action,
        Func<T, Task> done,
        CancellationToken cancellationToken = default)
    {
        batchBlock = new BatchBlock<T>(
            capacity,
            batchSize,
            batchFlushInterval,
            done: async batch => await actionBlock.Send(batch),
            cancellationToken);

        actionBlock = new ActionBlock<T[]>(
            capacity: 1,
            parallelism,
            action,
            done: async batch => {
                foreach (var item in batch)
                    await done(item);
            },
            cancellationToken);
    }

    public int InputCount => batchBlock.InputCount;

    public async Task Send(T item) => await batchBlock.Send(item);

    public async Task Complete()
    {
        await batchBlock.Complete();
        await actionBlock.Complete();
    }
}