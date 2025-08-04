using System.Threading.Channels;

namespace Simpipe.Net;

public class BatchBlock<T>(ChannelReader<T> reader, int batchSize, Action<T[]> done)
{
    public async Task RunAsync()
    {
        var batch = new List<T>();

        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var item))
            {
                batch.Add(item);
                
                if (batch.Count >= batchSize)
                {
                    done(batch.ToArray());
                    batch.Clear();
                }
            }
        }

        // Emit final incomplete batch if any items remain
        if (batch.Count > 0)
        {
            done(batch.ToArray());
        }
    }
}