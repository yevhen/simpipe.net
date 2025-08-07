namespace Simpipe.Blocks;

public interface IActionBlockExecutor<T>
{
    Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send);
    Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action);
    Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done);
}

internal class DefaultExecutor<T> : IActionBlockExecutor<T>
{
    public static DefaultExecutor<T> Instance { get; } = new();

    public Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send) => send.Execute(item);
    public Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action) => action.Execute(item);
    public Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done) => done.Execute(item);
}

public interface IItemCounter
{
    int InputCount { get; }
    int OutputCount { get; }
    int WorkingCount { get; }
}

internal class CountingExecutor<T> : IActionBlockExecutor<T>, IItemCounter
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
        Interlocked.Add(ref inputCount, -item.Size);
    }

    public async Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action)
    {
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