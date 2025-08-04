using System.Threading.Channels;
using NUnit.Framework;

namespace Simpipe.Net.Tests;

[TestFixture]
public class BatchActionBlockFixture
{
    [Test]
    public async Task BatchActionBlock_ProcessesBatchesWithAction()
    {
        var input = Channel.CreateUnbounded<int>();
        var processedItems = new List<int>();
        var lockObj = new object();
        
        // Composite: batches items, then processes each batch, then unpacks to individual items
        var batchAction = new BatchActionBlock<int>(
            input.Reader,
            batchSize: 3,
            parallelism: 2,
            batchAction: batch => {
                // Process the entire batch (e.g., bulk database operation)
                Console.WriteLine($"Processing batch of {batch.Length}");
                for (int i = 0; i < batch.Length; i++)
                    batch[i] *= 10; // Transform each item in batch
            },
            done: item => {
                lock (lockObj)
                {
                    processedItems.Add(item);
                }
            }); // Called for each individual item
        
        for (int i = 1; i <= 7; i++)
            await input.Writer.WriteAsync(i);
        input.Writer.Complete();
        
        await batchAction.RunAsync();
        
        Assert.AreEqual(7, processedItems.Count);
        Assert.Contains(10, processedItems); // 1 * 10
        Assert.Contains(20, processedItems); // 2 * 10  
        Assert.Contains(70, processedItems); // 7 * 10
    }
}