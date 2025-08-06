using Simpipe.Blocks;

namespace Simpipe.Tests.Blocks;

[TestFixture]
public class TimerBatchBlockFixture
{
    [Test]
    public async Task TimerBatchBlock_FlushesOnTimeout()
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

        Assert.That(batches.Count, Is.EqualTo(1)); 
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2}));

        await batchBlock.Complete();
    }

    [Test]
    public async Task TimerBatchBlock_FlushesOnSize()
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

        Assert.That(batches.Count, Is.EqualTo(3));
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2, 3}));
        Assert.That(batches[1], Is.EqualTo(new[] {4, 5, 6})); 
        Assert.That(batches[2], Is.EqualTo(new[] {7}));
    }

    [Test]
    public async Task TimerBatchBlock_DoesNotFlushByTimerIfRecentlyFlushedBySize()
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

        Assert.That(batches.Count, Is.EqualTo(1));
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2}));

        await batchBlock.Complete();
        
        Assert.That(batches.Count, Is.EqualTo(2));
        Assert.That(batches[1], Is.EqualTo(new[] {3}));
    }
}