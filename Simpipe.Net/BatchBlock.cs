using System.Threading.Channels;

namespace Simpipe.Net;

public class BatchBlock<T>(ChannelReader<T> input, int batchSize, TimeSpan flushInterval, Action<T[]> done)
{
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer = new(flushInterval);

    public async Task RunAsync()
    {
        await CollectBatch();
        FlushBuffer();
    }

    async Task CollectBatch()
    {
        var inputTask = input.WaitToReadAsync().AsTask();
        var timerTask = flushTimer.WaitForNextTickAsync().AsTask();

        while (!input.Completion.IsCompleted)
        {
            var completedTask = await Task.WhenAny(inputTask, timerTask);
            if (completedTask == inputTask)
            {
                if (await inputTask)
                {
                    while (input.TryRead(out var item))
                        FlushBySize(item);
                    
                    inputTask = input.WaitToReadAsync().AsTask();
                }
                else
                {
                    break;
                }
            }
            else if (completedTask == timerTask)
            {
                if (!await timerTask) continue;
                
                FlushBuffer();
                
                timerTask = flushTimer.WaitForNextTickAsync().AsTask();
            }
        }
        
        flushTimer.Dispose();
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