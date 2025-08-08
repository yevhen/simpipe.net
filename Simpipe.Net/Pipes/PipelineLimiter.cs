using System.Threading.Channels;
using Simpipe.Utility;

namespace Simpipe.Pipes;

public class PipelineLimiter<T>
{
    readonly Channel<T> input = Channel.CreateBounded<T>(1);
    readonly Channel<T> done = Channel.CreateBounded<T>(1);
    readonly Task processor;

    int wip;
    readonly int maxWork;
    readonly Func<T, Task> dispatch;

    public PipelineLimiter(int maxWork, Func<T, Task> dispatch)
    {
        this.maxWork = maxWork;
        this.dispatch = dispatch;

        processor = Select.Run(
            () => !input.Reader.Completion.IsCompleted &&
                  !done.Reader.Completion.IsCompleted,
            new Selector(() => input.Reader.WaitToReadAsync().AsTask(), ProcessSend),
            new Selector(() => done.Reader.WaitToReadAsync().AsTask(), ProcessDone));
    }

    async Task ProcessSend()
    {
        if (wip >= maxWork)
            return;

        if (input.Reader.TryRead(out var item))
        {
            wip++;
            await dispatch(item);
        }
    }

    async Task ProcessDone()
    {
        if (done.Reader.TryRead(out _))
            wip--;
    }

    public async Task Send(T item) => await input.Writer.WriteAsync(item);
    public async Task TrackDone(T item) => await done.Writer.WriteAsync(item);

    public async Task Complete()
    {
        input.Writer.Complete();
        done.Writer.Complete();
        await processor;
    }
}
