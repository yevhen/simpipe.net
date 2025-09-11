using static SharpAssert.Sharp;

namespace Simpipe.Blocks;

[TestFixture]
public class BatchBlockFixture
{
    [Test]
    public async Task Flushes_by_size()
    {
        var batches = new List<int[]>();
        
        var batchBlock = new BatchBlock<int>(
            capacity: 10,
            batchSize: 3,
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
}
