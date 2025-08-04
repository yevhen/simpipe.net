using System.Threading.Channels;

namespace Simpipe.Net;

public class BatchActionBlock<T>
{
    readonly BatchBlock<T> batchBlock;
    readonly ActionBlock<T[]> actionBlock;
    readonly Channel<T[]> intermediateChannel;

    public BatchActionBlock(ChannelReader<T> input, int batchSize, int parallelism, Action<T[]> batchAction, Action<T> done)
    {
        // Create intermediate channel between BatchBlock and ActionBlock
        intermediateChannel = Channel.CreateUnbounded<T[]>();
        
        // BatchBlock: T → T[] (batching only)
        batchBlock = new BatchBlock<T>(
            input, 
            batchSize, 
            flushInterval: TimeSpan.FromSeconds(1), // Default timeout
            done: batch => {
                try 
                {
                    intermediateChannel.Writer.TryWrite(batch);
                }
                catch (Exception)
                {
                    // Channel might be closed - ignore
                }
            });
        
        // ActionBlock: T[] → processed individual T items (action processing + unpacking)
        actionBlock = new ActionBlock<T[]>(
            intermediateChannel.Reader,
            parallelism: parallelism,
            action: processedBatch => {
                // Execute the batch action (e.g., bulk database operation)
                batchAction(processedBatch);
            },
            done: processedBatch => {
                // Unpack batch back to individual items and call done for each
                foreach (var item in processedBatch)
                    done(item);
            });
    }
    
    public async Task RunAsync()
    {
        // Start action block first
        var actionTask = actionBlock.RunAsync();
        
        // Run batch block and complete intermediate channel when done
        await batchBlock.RunAsync();
        intermediateChannel.Writer.Complete();
        
        // Wait for action block to finish processing all batches
        await actionTask;
    }
}