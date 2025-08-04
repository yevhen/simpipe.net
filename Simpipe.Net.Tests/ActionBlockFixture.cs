using System.Threading.Channels;

namespace Simpipe.Net.Tests;

public class TestItem
{
    public int Value { get; set; }
}

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

    [Test]
    public async Task Pipeline_LinksTwoBlocks()
    {
        var input = Channel.CreateUnbounded<int>();
        var intermediate = Channel.CreateUnbounded<int>();
        var result = 0;
        
        var multiply = new ActionBlock<int>(
            input.Reader,
            parallelism: 1,
            action: _ => Task.CompletedTask,
            done: async item => await intermediate.Writer.WriteAsync(item * 2));
            
        var store = new ActionBlock<int>(
            intermediate.Reader,
            parallelism: 1,
            action: item => result = item);
        
        await input.Writer.WriteAsync(21);
        input.Writer.Complete();
        
        // Run multiply first to completion to ensure intermediate channel gets completed
        await multiply.RunAsync();
        intermediate.Writer.Complete();
        
        await store.RunAsync();
        
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public async Task TransformBlock_MutatesInPlace()
    {
        var input = Channel.CreateUnbounded<TestItem>();
        var final = Channel.CreateUnbounded<TestItem>();
        var result = new List<TestItem>();
        
        var transform = new ActionBlock<TestItem>(
            input.Reader,
            action: item => item.Value *= 2,
            done: item => final.Writer.WriteAsync(item).AsTask());
            
        var collect = new ActionBlock<TestItem>(
            final.Reader,
            action: item => result.Add(item),
            done: _ => { });
        
        await input.Writer.WriteAsync(new TestItem { Value = 21 });
        input.Writer.Complete();
        
        // Run transform first to completion to ensure final channel gets completed
        await transform.RunAsync();
        final.Writer.Complete();
        
        await collect.RunAsync();
        
        Assert.That(result[0].Value, Is.EqualTo(42));
    }
}