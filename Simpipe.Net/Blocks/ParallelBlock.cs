using System.Diagnostics;

namespace Simpipe.Blocks;

public class ParallelBlock<T> : IActionBlock<T>
{
    readonly ActionBlock<T> input;
    readonly Dictionary<string, ActionBlock<T>> blockMap;
    readonly ActionBlock<T>[] blocks;
    readonly CompletionTrackingExecutor<T> completionTracker;
    readonly ParallelBlockCountingExecutor<T> blockCounters = new();

    public ParallelBlock(
        int capacity,
        int blockCount,
        IActionBlockExecutor<T> executor,
        Func<T, Task> done,
        Func<IActionBlockExecutor<T>, Dictionary<string, ActionBlock<T>>> blocksFactory,
        CancellationToken cancellationToken = default)
    {
        completionTracker = new CompletionTrackingExecutor<T>(blockCount, done, blockCounters);

        blockMap = blocksFactory(completionTracker);
        blocks = blockMap.Values.ToArray();

        blockCounters.SetBlocks(blocks);

        input = new ActionBlock<T>(
            capacity: capacity,
            parallelism: blockCount,
            BlockItemAction<T>.Async(item => Task.WhenAll(blocks.Select(block => block.Send(item)))),
            executor: executor,
            cancellationToken: cancellationToken);
    }

    public IItemCounter GetCounter(string blockId) => blockCounters.GetCounter(blockMap[blockId]);

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

internal class ParallelBlockCountingExecutor<T> : IActionBlockExecutor<T>
{
    readonly Dictionary<IActionBlock<T>, CountingExecutor<T>> blockCounters = new();

    public void SetBlocks(IEnumerable<IActionBlock<T>> blocks)
    {
        foreach (var block in blocks)
            blockCounters[block] = new CountingExecutor<T>();
    }

    public IItemCounter GetCounter(IActionBlock<T> block) => blockCounters[block];

    public async Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send) =>
        await blockCounters[block].ExecuteSend(block, item, send);

    public async Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action) =>
        await blockCounters[block].ExecuteAction(block, item, action);

    public async Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done) =>
        await blockCounters[block].ExecuteDone(block, item, done);
}