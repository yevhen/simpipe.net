using System.Threading.Channels;
using Simpipe.Utility;

namespace Simpipe;

public class BatchBlock<T> : IBlock<T>
{
    readonly Channel<T> input;
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer;
    readonly int batchSize;
    readonly Action<T[]> done;
    readonly Task processor;
    readonly CancellationToken cancellationToken;
    volatile bool batchFlushed;

    public BatchBlock(int capacity, int batchSize, TimeSpan flushInterval, Action<T[]> done, CancellationToken cancellationToken = default)
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

    void ProcessInput()
    {
        while (input.Reader.TryRead(out var item))
            FlushBySize(item);
    }

    void FlushBySize(T item)
    {
        batch.AddLast(item);

        if (batch.Count < batchSize)
            return;

        FlushBuffer();
        batchFlushed = true;
    }

    void ProcessTimer()
    {
        if (batchFlushed)
        {
            batchFlushed = false;
            return;
        }

        FlushBuffer();
    }

    void FlushBuffer()
    {
        if (batch.Count > 0)
            done(batch.ToArray());
        
        batch.Clear();
    }

    public async Task Send(T item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;

        flushTimer.Dispose();
        FlushBuffer();
    }
}