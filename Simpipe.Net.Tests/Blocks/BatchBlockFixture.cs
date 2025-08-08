namespace Simpipe.Blocks;

[TestFixture]
public class BatchBlockFixture
{
    [Test]
    public async Task BatchBlock_FlushesBySize()
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

        Assert.That(batches.Count, Is.EqualTo(3));
        Assert.That(batches[0], Is.EqualTo(new[] {1, 2, 3}));
        Assert.That(batches[1], Is.EqualTo(new[] {4, 5, 6})); 
        Assert.That(batches[2], Is.EqualTo(new[] {7}));
    }
}