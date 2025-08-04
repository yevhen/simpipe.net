using System.Threading.Channels;

namespace Simpipe.Net;

public class BatchBlock<T>(ChannelReader<T> reader, int batchSize, Action<T[]> done)
{
    readonly LinkedList<T> batch = [];
    
    public async Task RunAsync()
    {
        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var item)) 
                FlushBySize(item);
        }

        FlushBuffer();
    }

    void FlushBySize(T item)
    {
        batch.AddLast(item);

        if (batch.Count < batchSize) 
            return;

        FlushBuffer();
    }

    void FlushBuffer()
    {
        if (batch.Count > 0)
            done(batch.ToArray());
        
        batch.Clear();
    }
}