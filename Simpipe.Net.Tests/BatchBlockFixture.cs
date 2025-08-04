using System.Threading.Channels;

namespace Simpipe.Net.Tests;

[TestFixture]
public class BatchBlockFixture
{
    [Test]
    public async Task BatchBlock_OnlyCreatesBatches()
    {
        var input = Channel.CreateUnbounded<int>();
        var batches = new List<int[]>();
        
        // BatchBlock ONLY batches - no action processing
        var batchBlock = new BatchBlock<int>(
            input.Reader,
            batchSize: 3,
            done: batch => batches.Add(batch)); // Done called with T[]
        
        for (int i = 1; i <= 7; i++)
            await input.Writer.WriteAsync(i);
        input.Writer.Complete();
        
        await batchBlock.RunAsync();
        
        Assert.That(batches.Count, Is.EqualTo(3));
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2, 3}));
        Assert.That(batches[1], Is.EqualTo(new[] {4, 5, 6})); 
        Assert.That(batches[2], Is.EqualTo(new[] {7}));
    }
}