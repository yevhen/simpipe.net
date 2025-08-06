using System.Threading.Channels;

namespace Simpipe.Blocks;

public class BatchBlock<T>
{
    readonly Channel<T> input;
    readonly LinkedList<T> batch = [];
    readonly int batchSize;
    readonly Func<T[], Task> done;
    readonly Task processor;
    readonly CancellationToken cancellationToken;

    public BatchBlock(int capacity, int batchSize, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.batchSize = batchSize;
        this.done = done;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<T>(capacity);
        processor = Task.Run(ProcessChannel, cancellationToken);
    }

    public int InputCount => input.Reader.Count;

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            while (input.Reader.TryRead(out var item))
                await FlushBySize(item);
        }
    }

    async Task FlushBySize(T item)
    {
        batch.AddLast(item);

        if (batch.Count < batchSize)
            return;

        await FlushBuffer();
    }

    public async Task FlushBuffer()
    {
        if (batch.Count > 0)
            await done(batch.ToArray());
        
        batch.Clear();
    }

    public async Task Send(T item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;
        await FlushBuffer();
    }
}