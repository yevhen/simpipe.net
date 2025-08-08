namespace Simpipe.Blocks;

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
            parallelism: 1,
            action: BlockItemAction<int>.Sync(item => processed = item),
            done: BlockItemAction<int>.Sync(item => completed = item));

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
            action: BlockItemAction<int>.Async(async _ => {
                Interlocked.Increment(ref currentConcurrency);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
                await Task.Delay(50);
                Interlocked.Increment(ref processedCount);
                Interlocked.Decrement(ref currentConcurrency);
            }));

        for (var i = 0; i < 5; i++)
            await block.Send(i);
        await block.Complete();

        Assert.That(processedCount, Is.EqualTo(5));
        Assert.That(maxConcurrency, Is.GreaterThanOrEqualTo(2));
        Assert.That(maxConcurrency, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public async Task ActionBlock_StopsProcessingAfterException()
    {
        var processedItems = new List<int>();
        
        var block = new ActionBlock<int>(
            capacity: 10,
            parallelism: 1,
            action: BlockItemAction<int>.Sync(item =>
            {
                if (item == 2)
                    throw new ArgumentException("Test exception");
                processedItems.Add(item);
            }));

        await block.Send(1);
        await block.Send(2);
        await block.Send(3);

        Assert.ThrowsAsync<ArgumentException>(() => block.Complete());
        
        Assert.That(processedItems, Contains.Item(1), "Only item 1 should be processed before the exception occurs");
        Assert.That(processedItems, Does.Not.Contain(2));
        Assert.That(processedItems, Does.Not.Contain(3));
    }
}