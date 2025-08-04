using System.Threading.Channels;

namespace Simpipe.Net;

internal record Selector(Func<Task<bool>> Waiter, Action Execute);

internal static class Select
{
    public static async Task Run(Selector[] selectors, Func<bool> runUntil)
    {
        var tasks = selectors.Select(s => s.Waiter()).ToArray();
        
        while (runUntil())
        {
            var completedTask = await Task.WhenAny(tasks);
            if (!await completedTask)
                break;
            
            var index = Array.IndexOf(tasks, completedTask);
            var selector = selectors[index];

            selector.Execute();
            tasks[index] = selector.Waiter();
        }
    }
}

public class BatchBlock<T>(ChannelReader<T> input, int batchSize, TimeSpan flushInterval, Action<T[]> done)
{
    readonly LinkedList<T> batch = [];
    readonly PeriodicTimer flushTimer = new(flushInterval);

    public async Task RunAsync()
    {
        await FlushBatch();
        FlushBuffer();
    }

    async Task FlushBatch()
    {
        await Select.Run([
            new Selector(() => input.WaitToReadAsync().AsTask(), ProcessInput),
            new Selector(() => flushTimer.WaitForNextTickAsync().AsTask(), FlushBuffer)
        ], () => !input.Completion.IsCompleted);
        
        flushTimer.Dispose();
    }

    void ProcessInput()
    {
        while (input.TryRead(out var item))
        {
            batch.AddLast(item);

            if (batch.Count < batchSize)
                return;

            FlushBuffer();
        }
    }

    void FlushBuffer()
    {
        if (batch.Count > 0)
            done(batch.ToArray());
        
        batch.Clear();
    }
}