using System.Threading.Channels;

namespace Simpipe.Blocks;

[TestFixture]
public class BatchActionBlockFixture
{
    [Test]
    public async Task BatchActionBlock_ProcessesBatchesWithAction()
    {
        const int itemCount = 4;
        var batches = Channel.CreateUnbounded<string[]>();

        var batchActionBlock = new BatchActionBlock<string>(
            capacity: 4,
            batchSize: 2,
            batchFlushInterval: TimeSpan.FromMinutes(1),
            parallelism: 1,
            action: BlockItemAction<string>.BatchAsync(async batch => await batches.Writer.WriteAsync(batch)),
            done: BlockItemAction<string>.BatchAsync(_ => Task.CompletedTask));
        
        for (var i = 1; i <= itemCount; i++)
            await batchActionBlock.Send($"i{i}");
        
        var batch1 = await batches.Reader.ReadAsync();
        var batch2 = await batches.Reader.ReadAsync();

        await batchActionBlock.Complete();

        Assert.That(batch1, Is.EqualTo(new[] {"i1", "i2"}));
        Assert.That(batch2, Is.EqualTo(new[] {"i3", "i4"}));
    }
}