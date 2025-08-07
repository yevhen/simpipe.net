using System.Threading.Channels;

namespace Simpipe.Blocks;

public interface IActionBlockExecutor<T>
{
    Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send);
    Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action);
    Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done);
}

internal class DefaultActionBlockExecutor<T> : IActionBlockExecutor<T>
{
    public static DefaultActionBlockExecutor<T> Instance { get; } = new();

    public Task ExecuteSend(BlockItem<T> item, BlockItemAction<T> send) => send.Execute(item);
    public Task ExecuteAction(BlockItem<T> item, BlockItemAction<T> action) => action.Execute(item);
    public Task ExecuteDone(BlockItem<T> item, BlockItemAction<T> done) => done.Execute(item);
}

public interface IActionBlock<T>
{
    Task Send(BlockItem<T> item);
    Task Complete();
}

public class ActionBlock<T> : IActionBlock<T>
{
    readonly Channel<BlockItem<T>> input;
    readonly BlockItemAction<T> send;
    readonly BlockItemAction<T> action;
    readonly BlockItemAction<T> done;
    readonly IActionBlockExecutor<T> executor;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    public ActionBlock(
        int capacity,
        int parallelism,
        BlockItemAction<T> action,
        BlockItemAction<T> done,
        IActionBlockExecutor<T>? executor = null,
        CancellationToken cancellationToken = default)
    {
        this.action = action;
        this.done = done;
        this.cancellationToken = cancellationToken;
        this.executor = executor ?? DefaultActionBlockExecutor<T>.Instance;

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
        await executor.ExecuteAction(item, action);

        if (!cancellationToken.IsCancellationRequested)
            await executor.ExecuteDone(item, done);
    }

    public async Task Send(BlockItem<T> item) => await executor.ExecuteSend(item, send);

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