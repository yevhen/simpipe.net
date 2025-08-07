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

internal class CountingExecutor<T> : IActionBlockExecutor<T>
{
    volatile int inputCount;
    volatile int outputCount;
    volatile int workingCount;

    public int InputCount => inputCount;
    public int OutputCount => outputCount;
    public int WorkingCount => workingCount;

    public async Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send)
    {
        Interlocked.Increment(ref inputCount);
        await send.Execute(item);
        Interlocked.Decrement(ref inputCount);
    }

    public async Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action)
    {
        Interlocked.Increment(ref workingCount);
        await action.Execute(item);
        Interlocked.Decrement(ref workingCount);
    }

    public async Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done)
    {
        Interlocked.Increment(ref outputCount);
        await done.Execute(item);
        Interlocked.Decrement(ref outputCount);
    }
}