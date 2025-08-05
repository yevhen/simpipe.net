using System.Threading.Channels;
using Simpipe.Net.Utility;

namespace Simpipe.Net;

public class BatchBlock<T> : IBlock<T>
{
    readonly Channel<T> input;
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer;
    readonly int batchSize;
    readonly Action<T[]> done;
    readonly Task processor;
    volatile bool batchFlushed;

    public BatchBlock(int capacity, int batchSize, TimeSpan flushInterval, Action<T[]> done)
    {
        this.batchSize = batchSize;
        this.done = done;

        flushTimer = new PeriodicTimer(flushInterval);
        input = Channel.CreateBounded<T>(capacity);

        processor = Select
            .When(() => input.Reader.WaitToReadAsync().AsTask(), ProcessInput)
            .When(() => flushTimer.WaitForNextTickAsync().AsTask(), ProcessTimer)
            .RunUntil(() => !input.Reader.Completion.IsCompleted);
    }

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

    public async Task Send(T item) => await input.Writer.WriteAsync(item);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;

        flushTimer.Dispose();
        FlushBuffer();
    }
}