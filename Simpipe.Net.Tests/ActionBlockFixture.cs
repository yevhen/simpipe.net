using System.Threading.Channels;
using Simpipe.Channels;

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
        
        Assert.AreEqual(42, processed);
        Assert.AreEqual(42, completed);
    }
}