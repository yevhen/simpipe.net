using System.Threading.Channels;

namespace Simpipe.Net;

public class ActionBlock<T>(ChannelReader<T> reader, int parallelism, Func<T, Task> action, Func<T, Task>? done = null)
{
    private readonly Func<T, Task> done = done ?? (_ => Task.CompletedTask);

    public ActionBlock(ChannelReader<T> reader, Action<T> action, Action<T> done)
        : this(reader, parallelism: 1, action: action, done: done)
    {}

    public ActionBlock(ChannelReader<T> reader, int parallelism, Action<T> action, Action<T>? done = null)
        : this(reader, parallelism, 
            item => { action(item); return Task.CompletedTask; }, 
            done != null ? item => { done(item); return Task.CompletedTask; } : null)
    {}

    public Task RunAsync() => Task.WhenAll(Enumerable.Range(0, parallelism).Select(_ => Task.Run(ProcessChannel)));

    private async Task ProcessChannel()
    {
        while (await reader.WaitToReadAsync())
        {
            if (reader.TryRead(out var item))
                await ProcessItem(item);
        }
    }

    private async Task ProcessItem(T item)
    {
        await action(item);
        await done(item);
    }
}