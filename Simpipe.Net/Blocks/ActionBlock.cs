using System.Threading.Channels;

namespace Simpipe;

public class ActionBlock<T> : IBlock<T>
{
    readonly Func<T, Task> done;
    readonly Channel<T> input;
    readonly Func<T, Task> action;
    readonly Task processor;
    readonly CancellationToken cancellationToken;

    public ActionBlock(int capacity, Action<T> action, Action<T> done, CancellationToken cancellationToken = default)
        : this(capacity, parallelism: 1, action: action, done: done, cancellationToken: cancellationToken)
    {}

    public ActionBlock(int capacity, int parallelism, Action<T> action, Action<T>? done = null, CancellationToken cancellationToken = default)
        : this(capacity, parallelism,
            item => { action(item); return Task.CompletedTask; },
            done != null ? item => { done(item); return Task.CompletedTask; } : null,
            cancellationToken)
    {}

    public ActionBlock(int capacity, int parallelism, Func<T, Task> action, Func<T, Task>? done = null, CancellationToken cancellationToken = default)
    {
        this.action = action;
        this.done = done ?? (_ => Task.CompletedTask);
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<T>(capacity);
        processor = Task.WhenAll(Enumerable.Range(0, parallelism).Select(_ => Task.Run(ProcessChannel, cancellationToken)));
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

    async Task ProcessItem(T item)
    {
        await action(item);

        if (!cancellationToken.IsCancellationRequested)
            await done(item);
    }

    public async Task Send(T item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;
    }
}