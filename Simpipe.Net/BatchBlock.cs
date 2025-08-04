using System.Threading.Channels;

namespace Simpipe.Net;

public class BatchBlock<T>
{
    readonly ChannelReader<T> reader;
    readonly int batchSize;
    readonly Action<T[]> done;
    readonly TimeSpan? flushInterval;
    readonly LinkedList<T> batch = [];
    readonly Timer? flushTimer;
    
    public BatchBlock(ChannelReader<T> reader, int batchSize, Action<T[]> done)
    {
        this.reader = reader;
        this.batchSize = batchSize;
        this.done = done;
        this.flushInterval = null;
        this.flushTimer = null;
    }
    
    public BatchBlock(ChannelReader<T> reader, int batchSize, TimeSpan flushInterval, Action<T[]> done)
    {
        this.reader = reader;
        this.batchSize = batchSize;
        this.done = done;
        this.flushInterval = flushInterval;
        this.flushTimer = new Timer(OnFlushTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
    
    public async Task RunAsync()
    {
        try
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var item)) 
                    FlushBySize(item);
            }

            FlushBuffer();
        }
        finally
        {
            flushTimer?.Dispose();
        }
    }

    void FlushBySize(T item)
    {
        batch.AddLast(item);

        // Start timer on first item if flush interval is configured
        if (batch.Count == 1 && flushInterval.HasValue)
        {
            flushTimer?.Change(flushInterval.Value, Timeout.InfiniteTimeSpan);
        }

        if (batch.Count < batchSize) 
            return;

        FlushBuffer();
    }

    void FlushBuffer()
    {
        if (batch.Count > 0)
        {
            // Stop timer when flushing
            flushTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            done(batch.ToArray());
        }
        
        batch.Clear();
    }
    
    void OnFlushTimer(object? state)
    {
        FlushBuffer();
    }
}