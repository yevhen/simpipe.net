using System.Threading.Channels;
using static SharpAssert.Sharp;

namespace Simpipe.Blocks;

[TestFixture]
public class BatchActionBlockFixture
{
    [Test]
    public async Task Processes_batches_with_action()
    {
        const int itemCount = 4;
        var batches = Channel.CreateUnbounded<string[]>();

        var batchActionBlock = new BatchActionBlock<string>(
            capacity: 4,
            batchSize: 2,
            batchFlushInterval: TimeSpan.FromMinutes(1),
            parallelism: 1,
            action: BlockItemAction<string>.BatchAsync(async batch => await batches.Writer.WriteAsync(batch)));
        
        for (var i = 1; i <= itemCount; i++)
            await batchActionBlock.Send($"i{i}");
        
        var batch1 = await batches.Reader.ReadAsync();
        var batch2 = await batches.Reader.ReadAsync();

        await batchActionBlock.Complete();

        Assert(batch1.SequenceEqual(new[] {"i1", "i2"}));
        Assert(batch2.SequenceEqual(new[] {"i3", "i4"}));
    }

    [Test]
    public async Task Stops_processing_after_exception_in_batch()
    {
        var processedBatches = new List<string[]>();
        
        var batchActionBlock = new BatchActionBlock<string>(
            capacity: 10,
            batchSize: 2,
            batchFlushInterval: TimeSpan.FromMinutes(1),
            parallelism: 1,
            action: BlockItemAction<string>.BatchSync(batch =>
            {
                if (batch.Contains("error"))
                    throw new ArgumentException("Test exception");
                processedBatches.Add(batch);
            }));

        await batchActionBlock.Send("item1");
        await batchActionBlock.Send("item2");
        await batchActionBlock.Send("error");
        await batchActionBlock.Send("item4");

        Assert(await ThrowsAsync<ArgumentException>(() => batchActionBlock.Complete()));
        
        Assert(processedBatches.Count == 1, "Only the first batch should be processed before the exception");
        Assert(processedBatches[0].SequenceEqual(new[] {"item1", "item2"}));

        Assert(!processedBatches.Any(b => b.Contains("error")),
            "The second batch containing \"error\" should not be in the processed list");
    }
}