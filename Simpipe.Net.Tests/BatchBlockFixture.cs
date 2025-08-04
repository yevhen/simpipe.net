using System.Threading.Channels;

namespace Simpipe.Net.Tests;

[TestFixture]
public class BatchBlockFixture
{
    [Test]
    public async Task BatchBlock_FlushesBySize()
    {
        var input = Channel.CreateUnbounded<int>();
        var batches = new List<int[]>();
        
        var batchBlock = new BatchBlock<int>(
            input.Reader,
            batchSize: 3,
            flushInterval: TimeSpan.FromMinutes(1),
            done: batch => batches.Add(batch));
        
        for (int i = 1; i <= 7; i++)
            await input.Writer.WriteAsync(i);
        input.Writer.Complete();
        
        await batchBlock.RunAsync();
        
        Assert.That(batches.Count, Is.EqualTo(3));
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2, 3}));
        Assert.That(batches[1], Is.EqualTo(new[] {4, 5, 6})); 
        Assert.That(batches[2], Is.EqualTo(new[] {7}));
    }

    [Test]
    public async Task BatchBlock_FlushesOnTimeout()
    {
        var input = Channel.CreateUnbounded<int>();
        var batches = new List<int[]>();
        
        var batch = new BatchBlock<int>(
            input.Reader,
            batchSize: 10,
            flushInterval: TimeSpan.FromMilliseconds(100),
            done: b => batches.Add(b));
        
        await input.Writer.WriteAsync(1);
        await input.Writer.WriteAsync(2);
        
        var batchTask = batch.RunAsync();
        
        await Task.Delay(150);
        input.Writer.Complete();
        
        await batchTask;
        
        Assert.That(batches.Count, Is.EqualTo(1)); 
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2}));
    }
}