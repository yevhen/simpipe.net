namespace Simpipe.Blocks;

internal class MetricsTrackingExecutor<T>
{
    volatile int inputCount;
    volatile int outputCount;
    volatile int workingCount;

    public int InputCount => inputCount;
    public int OutputCount => outputCount;
    public int WorkingCount => workingCount;

    public async Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send)
    {
        Interlocked.Add(ref inputCount, item.Size);

        await send.Execute(item);
    }

    public async Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action)
    {
        Interlocked.Add(ref inputCount, -item.Size);
        Interlocked.Add(ref workingCount, item.Size);

        await action.Execute(item);

        Interlocked.Add(ref workingCount, -item.Size);
    }

    public async Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done)
    {
        Interlocked.Add(ref outputCount, item.Size);

        await done.Execute(item);

        Interlocked.Add(ref outputCount, -item.Size);
    }
}