using System.Threading.Channels;

namespace Simpipe.Net.Tests;

[TestFixture]
public class ActionBlockFixture
{
    [Test]
    public async Task ActionBlock_ProcessesSingleItem()
    {
        var input = Channel.CreateUnbounded<int>();
        var processed = 0;
        var completed = 0;
        
        var block = new ActionBlock<int>(
            input.Reader,
            action: item => processed = item,
            done: item => completed = item);
        
        await input.Writer.WriteAsync(42);
        input.Writer.Complete();
        
        await block.RunAsync();

        Assert.That(processed, Is.EqualTo(42));
        Assert.That(completed, Is.EqualTo(42));
    }

    [Test]
    public async Task ActionBlock_ProcessesInParallel()
    {
        var channel = Channel.CreateUnbounded<int>();
        var processedCount = 0;
        var maxConcurrency = 0;
        var currentConcurrency = 0;
        
        var block = new ActionBlock<int>(
            channel.Reader,
            parallelism: 3, 
            action: async item => {
                Interlocked.Increment(ref currentConcurrency);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);
                await Task.Delay(50);
                Interlocked.Increment(ref processedCount);
                Interlocked.Decrement(ref currentConcurrency);
            });
        
        // Write 5 items
        for (int i = 0; i < 5; i++)
            await channel.Writer.WriteAsync(i);
        channel.Writer.Complete();
        
        await block.RunAsync();

        Assert.That(processedCount, Is.EqualTo(5));
        Assert.That(maxConcurrency, Is.GreaterThanOrEqualTo(2));
        Assert.That(maxConcurrency, Is.LessThanOrEqualTo(3));
    }
}