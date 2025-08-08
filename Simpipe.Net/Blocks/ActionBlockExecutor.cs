namespace Simpipe.Blocks;

public interface IActionBlockExecutor<T>
{
    Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send);
    Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action);
    Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done);
}

internal class DefaultExecutor<T> : IActionBlockExecutor<T>
{
    public static DefaultExecutor<T> Instance { get; } = new();

    public Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send) => send.Execute(item);
    public Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action) => action.Execute(item);
    public Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done) => done.Execute(item);}

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

    public async Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send)
    {
        Interlocked.Add(ref inputCount, item.Size);
        await send.Execute(item);
    }

    public async Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action)
    {
        Interlocked.Add(ref inputCount, -item.Size);
        Interlocked.Add(ref workingCount, item.Size);
        await action.Execute(item);
        Interlocked.Add(ref workingCount, -item.Size);
    }

    public async Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done)
    {
        Interlocked.Add(ref outputCount, item.Size);
        await done.Execute(item);
        Interlocked.Add(ref outputCount, -item.Size);
    }
}