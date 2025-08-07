using System.Threading.Channels;

namespace Simpipe.Blocks;

public interface IActionBlock<T>
{
    Task Send(BlockItem<T> item);
    Task Complete();
}

public class ActionBlock<T> : IActionBlock<T>
{
    readonly Channel<BlockItem<T>> input;
    readonly BlockItemAction<T> action;
    readonly BlockItemAction<T> done;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    public ActionBlock(
        int capacity,
        int parallelism,
        BlockItemAction<T> action,
        BlockItemAction<T> done,
        CancellationToken cancellationToken = default)
    {
        this.action = action;
        this.done = done;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<BlockItem<T>>(capacity);
        processor = Task.WhenAll(Enumerable
            .Range(0, parallelism)
            .Select(_ => Task.Run(ProcessChannel, cancellationToken)));
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
        await action.Execute(item);

        if (!cancellationToken.IsCancellationRequested)
            await done.Execute(item);
    }

    public async Task Send(BlockItem<T> item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;
    }
}

public static class ActionBlockExtensions
{
    public static Task Send<T>(this IActionBlock<T> block, T item) => block.Send(new BlockItem<T>(item));
    public static Task Send<T>(this IActionBlock<T> block, T[] items) => block.Send(new BlockItem<T>(items));
}