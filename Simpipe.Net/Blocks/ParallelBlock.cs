using System.Diagnostics;

namespace Simpipe.Blocks;

public class ParallelBlock<T> : IActionBlock<T>
{
    readonly ActionBlock<T> input;
    readonly Dictionary<string, IActionBlock<T>> blocks;
    readonly CompletionTracker<T> completion;

    public ParallelBlock(
        int capacity,
        int blockCount,
        Func<T, Task> done,
        Func<BlockItemAction<T>, Dictionary<string, IActionBlock<T>>> blocksFactory,
        CancellationToken cancellationToken = default)
    {
        completion = new CompletionTracker<T>(blockCount, done);

        blocks = blocksFactory(new BlockItemAction<T>(completion.TrackDone));

        input = new ActionBlock<T>(
            capacity: capacity,
            parallelism: blockCount,
            BlockItemAction<T>.Async(SendAll),
            cancellationToken: cancellationToken);
    }

    public IEnumerable<KeyValuePair<string, IActionBlock<T>>> Blocks => blocks;

    public async Task Send(BlockItem<T> item) => await input.Send(item);

    public async Task Complete()
    {
        await input.Complete();

        await CompleteAll();

        await completion.Complete();
    }

    Task SendAll(T item) => Task.WhenAll(blocks.Values.Select(block => block.Send(item)));
    Task CompleteAll() => Task.WhenAll(blocks.Values.Select(block => block.Complete()));
}

internal class CompletionTracker<T>
{
    readonly ActionBlock<T> completion;
    readonly Dictionary<object, int> completed = new();
    readonly int blockCount;
    readonly Func<T, Task> done;

    public CompletionTracker(int blockCount, Func<T, Task> done)
    {
        this.blockCount = blockCount;
        this.done = done;

        completion = new ActionBlock<T>(capacity: 1, parallelism: 1, BlockItemAction<T>.Async(ReportDone));
    }

    async Task ReportDone(T item)
    {
        Debug.Assert(item != null, nameof(item) + " != null");

        if (completed.TryGetValue(item, out var currentCount))
            completed[item] = currentCount + 1;
        else
            completed[item] = 1;

        if (completed[item] == blockCount)
            await done(item);
    }

    public async Task TrackDone(BlockItem<T> item)
    {
        await completion.Send(item);
    }

    public Task Complete() => completion.Complete();
}