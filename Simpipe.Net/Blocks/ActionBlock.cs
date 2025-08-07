using System.Threading.Channels;

namespace Simpipe.Blocks;

public class ActionBlock<T> : IBlock<T>
{
    BlockItemAction<T> action;
    BlockItemAction<T> done;

    readonly CancellationToken cancellationToken;
    readonly Channel<BlockItem<T>> input;
    readonly Task processor;

    public ActionBlock(
        int capacity,
        int parallelism,
        Func<BlockItem<T>, Task> action,
        Func<BlockItem<T>, Task> done,
        CancellationToken cancellationToken = default)
    {
        SetAction(action);
        SetDone(done);

        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<BlockItem<T>>(capacity);
        processor = Task.WhenAll(Enumerable
            .Range(0, parallelism)
            .Select(_ => Task.Run(ProcessChannel, cancellationToken)));
    }

    public int InputCount => input.Reader.Count;

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

    public void SetAction(Func<BlockItem<T>, Task> action) => this.action = new(action);
    public void SetDone(Func<BlockItem<T>, Task> done) => this.done = new(done);
}