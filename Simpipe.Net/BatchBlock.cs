using System.Threading.Channels;
using Simpipe.Net.Utility;

namespace Simpipe.Net;

public class BatchBlock<T>(ChannelReader<T> input, int batchSize, TimeSpan flushInterval, Action<T[]> done)
{
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer = new(flushInterval);

    public async Task RunAsync()
    {
        await ProcessItems();
        FlushBuffer();
        Dispose();
    }

    Task ProcessItems() => Select
        .When(() => input.WaitToReadAsync().AsTask(), ProcessInput)
        .When(() => flushTimer.WaitForNextTickAsync().AsTask(), ProcessTimer)
        .RunUntil(() => !input.Completion.IsCompleted);

    void ProcessInput()
    {
        while (input.TryRead(out var item))
            FlushBySize(item);
    }

    void FlushBySize(T item)
    {
        batch.AddLast(item);

        if (batch.Count < batchSize)
            return;

        FlushBuffer();
    }


    void ProcessTimer() => FlushBuffer();

    void FlushBuffer()
    {
        if (batch.Count > 0)
            done(batch.ToArray());
        
        batch.Clear();
    }

    void Dispose() => flushTimer.Dispose();
}