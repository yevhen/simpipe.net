namespace Simpipe.Blocks;

using static SharpAssert.Sharp;

[TestFixture]
public class TimerBatchBlockFixture
{
    [Test]
    public async Task Flushes_on_timeout()
    {
        var batches = new List<int[]>();
        
        var batchBlock = new TimerBatchBlock<int>(
            capacity: 10,
            batchSize: 10,
            flushInterval: TimeSpan.FromMilliseconds(100),
            done: batch =>
            {
                batches.Add(batch);
                return Task.CompletedTask;
            });
        
        await batchBlock.Send(1);
        await batchBlock.Send(2);
        
        await Task.Delay(150);

        Assert(batches.Count == 1); 
        Assert(batches[0].SequenceEqual(new[] {1, 2}));

        await batchBlock.Complete();
    }

    [Test]
    public async Task Flushes_on_size()
    {
        var batches = new List<int[]>();
        
        var batchBlock = new TimerBatchBlock<int>(
            capacity: 10,
            batchSize: 3,
            flushInterval: TimeSpan.FromMinutes(1),
            done: batch =>
            {
                batches.Add(batch);
                return Task.CompletedTask;
            });
        
        for (var i = 1; i <= 7; i++)
            await batchBlock.Send(i);
        await batchBlock.Complete();

        Assert(batches.Count == 3);
        Assert(batches[0].SequenceEqual(new[] {1, 2, 3}));
        Assert(batches[1].SequenceEqual(new[] {4, 5, 6})); 
        Assert(batches[2].SequenceEqual(new[] {7}));
    }

    [Test]
    public async Task Does_not_flush_by_timer_if_recently_flushed_by_size()
    {
        var batches = new List<int[]>();
        
        var batchBlock = new TimerBatchBlock<int>(
            capacity: 10,
            batchSize: 2,
            flushInterval: TimeSpan.FromMilliseconds(500),
            done: batch =>
            {
                batches.Add(batch);
                return Task.CompletedTask;
            });
        
        await batchBlock.Send(1);
        await batchBlock.Send(2);
        await batchBlock.Send(3);
        
        await Task.Delay(750);

        Assert(batches.Count == 1);
        Assert(batches[0].SequenceEqual(new[] {1, 2}));

        await batchBlock.Complete();
        
        Assert(batches.Count == 2);
        Assert(batches[1].SequenceEqual(new[] {3}));
    }
}
