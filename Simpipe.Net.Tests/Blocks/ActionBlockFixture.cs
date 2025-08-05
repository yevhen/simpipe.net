namespace Simpipe.Tests.Blocks;

[TestFixture]
public class ActionBlockFixture
{
    [Test]
    public async Task ActionBlock_ProcessesSingleItem()
    {
        var processed = 0;
        var completed = 0;
        
        var block = new ActionBlock<int>(
            capacity: 1,
            action: item => processed = item,
            done: item => completed = item);

        await block.Send(42);
        await block.Complete();

        Assert.That(processed, Is.EqualTo(42));
        Assert.That(completed, Is.EqualTo(42));
    }

    [Test]
    public async Task ActionBlock_ProcessesInParallel()
    {
        var processedCount = 0;
        var maxConcurrency = 0;
        var currentConcurrency = 0;
        
        var block = new ActionBlock<int>(
            capacity: 10,
            parallelism: 3, 
            action: async item => {
                Interlocked.Increment(ref currentConcurrency);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
                await Task.Delay(50);
                Interlocked.Increment(ref processedCount);
                Interlocked.Decrement(ref currentConcurrency);
            });

        for (var i = 0; i < 5; i++)
            await block.Send(i);
        await block.Complete();

        Assert.That(processedCount, Is.EqualTo(5));
        Assert.That(maxConcurrency, Is.GreaterThanOrEqualTo(2));
        Assert.That(maxConcurrency, Is.LessThanOrEqualTo(3));
    }
}