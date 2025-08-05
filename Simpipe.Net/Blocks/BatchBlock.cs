using System.Threading.Channels;
using Simpipe.Utility;

namespace Simpipe;

public class BatchBlock<T> : IBlock<T>
{
    readonly Channel<T> input;
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer;
    readonly int batchSize;
    readonly Func<T[], Task> done;
    readonly Task processor;
    readonly CancellationToken cancellationToken;
    volatile bool batchFlushed;

    public BatchBlock(int capacity, int batchSize, TimeSpan flushInterval, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.batchSize = batchSize;
        this.done = done;
        this.cancellationToken = cancellationToken;

        flushTimer = new PeriodicTimer(flushInterval);
        input = Channel.CreateBounded<T>(capacity);

        processor = Select
            .When(() => input.Reader.WaitToReadAsync(cancellationToken).AsTask(), ProcessInput)
            .When(() => flushTimer.WaitForNextTickAsync(cancellationToken).AsTask(), ProcessTimer)
            .RunUntil(() => !input.Reader.Completion.IsCompleted);
    }

    public int InputCount => input.Reader.Count;

    async Task ProcessInput()
    {
        while (input.Reader.TryRead(out var item))
            await FlushBySize(item);
    }

    async Task FlushBySize(T item)
    {
        batch.AddLast(item);

        if (batch.Count < batchSize)
            return;

        await FlushBuffer();
        batchFlushed = true;
    }

    async Task ProcessTimer()
    {
        if (batchFlushed)
        {
            batchFlushed = false;
            return;
        }

        await FlushBuffer();
    }

    async Task FlushBuffer()
    {
        if (batch.Count > 0)
            await done(batch.ToArray());
        
        batch.Clear();
    }

    public async Task Send(T item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;

        flushTimer.Dispose();
        await FlushBuffer();
    }
}