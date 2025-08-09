using System.Threading.Channels;

namespace Simpipe.Blocks;

public interface IActionBlock<T> : IBlock
{
    Task Send(BlockItem<T> item);
    Task Complete();
}

public class ActionBlock<T> : IActionBlock<T>
{
    readonly BlockMetrics<T> metrics = new();
    readonly Channel<BlockItem<T>> input;
    readonly BlockItemAction<T> send;
    readonly BlockItemAction<T> action;
    readonly BlockItemAction<T> done;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    public ActionBlock(
        int capacity,
        int parallelism,
        BlockItemAction<T> action,
        BlockItemAction<T>? done = null,
        CancellationToken cancellationToken = default)
    {
        this.action = action;
        this.done = done ?? BlockItemAction<T>.Noop;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<BlockItem<T>>(capacity);

        processor = Task.WhenAll(Enumerable
            .Range(0, parallelism)
            .Select(_ => Task.Run(ProcessChannel, cancellationToken)));

        send = new BlockItemAction<T>(async item =>
            await input.Writer.WriteAsync(item, cancellationToken));
    }

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            if (input.Reader.TryRead(out var item))
                await ProcessItem(item);
        }
    }

    async Task ProcessItem(BlockItem<T> item)
    {
        await Execute(item);
        await Done(item);
    }

    async Task Execute(BlockItem<T> item)
    {
        metrics.TrackExecute(item);

        await action.Execute(item);
    }

    async Task Done(BlockItem<T> item)
    {
        metrics.TrackDone(item);

        if (!cancellationToken.IsCancellationRequested)
            await done.Execute(item);

        metrics.TrackGone(item);
    }

    public async Task Send(BlockItem<T> item)
    {
        metrics.TrackSend(item);

        await send.Execute(item);
    }

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;
    }

    public int InputCount => metrics.InputCount;
    public int OutputCount => metrics.OutputCount;
    public int WorkingCount => metrics.WorkingCount;
}

public static class ActionBlockExtensions
{
    public static Task Send<T>(this IActionBlock<T> block, T item) => block.Send(new BlockItem<T>(item));
    public static Task Send<T>(this IActionBlock<T> block, T[] items) => block.Send(new BlockItem<T>(items));
}