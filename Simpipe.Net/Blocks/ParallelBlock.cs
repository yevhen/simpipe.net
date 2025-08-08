using System.Diagnostics;

namespace Simpipe.Blocks;

public class ParallelBlock<T> : IActionBlock<T>
{
    readonly ActionBlock<T> input;
    readonly List<IActionBlock<T>> blocks;
    readonly CompletionTrackingExecutor<T> completionTracker;

    public ParallelBlock(
        int capacity,
        int blockCount,
        IActionBlockExecutor<T> executor,
        Func<T, Task> done,
        Func<IActionBlockExecutor<T>, List<IActionBlock<T>>> blocksFactory,
        CancellationToken cancellationToken = default)
    {
        completionTracker = new CompletionTrackingExecutor<T>(blockCount, done, DefaultExecutor<T>.Instance);

        blocks = blocksFactory(completionTracker);
        if (blocks.Count != blockCount)
            throw new ArgumentException($"Expected {blockCount} blocks, but got {blocks.Count}.");

        input = new ActionBlock<T>(
            capacity: capacity,
            parallelism: blockCount,
            BlockItemAction<T>.Async(item => Task.WhenAll(blocks.Select(block => block.Send(item)))),
            executor: executor,
            cancellationToken: cancellationToken);
    }

    public async Task Send(BlockItem<T> item) => await input.Send(item);

    public async Task Complete()
    {
        await input.Complete();
        await Task.WhenAll(blocks.Select(block => block.Complete()));
        await completionTracker.Complete();
    }
}

internal class CompletionTrackingExecutor<T> : IActionBlockExecutor<T>
{
    readonly ActionBlock<T> completion;
    readonly Dictionary<object, int> completed = new();
    readonly int blockCount;
    readonly Func<T, Task> done;
    readonly IActionBlockExecutor<T> executor;

    public CompletionTrackingExecutor(int blockCount, Func<T, Task> done, IActionBlockExecutor<T> executor)
    {
        this.blockCount = blockCount;
        this.done = done;
        this.executor = executor;

        completion = new ActionBlock<T>(capacity: 1, parallelism: 1, BlockItemAction<T>.Async(TrackDone));
    }

    async Task TrackDone(T item)
    {
        Debug.Assert(item != null, nameof(item) + " != null");

        if (completed.TryGetValue(item, out var currentCount))
            completed[item] = currentCount + 1;
        else
            completed[item] = 1;

        if (completed[item] == blockCount)
            await done(item);
    }

    public Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send) =>
        executor.ExecuteSend(block, item, send);

    public Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action) =>
        executor.ExecuteAction(block, item, action);

    public async Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done)
    {
        await executor.ExecuteDone(block, item, done);
        await completion.Send(item);
    }

    public Task Complete() => completion.Complete();
}
