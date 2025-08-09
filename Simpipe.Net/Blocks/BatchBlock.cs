using System.Threading.Channels;

namespace Simpipe.Blocks;

public class BatchBlock<T> : IBlock
{
    readonly BlockMetrics<T> metrics = new();
    readonly Channel<T> input;
    readonly LinkedList<T> buffer = [];
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

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            while (input.Reader.TryRead(out var item))
            {
                metrics.TrackExecute(new BlockItem<T>(item));

                await FlushBySize(item);
            }
        }
    }

    async Task FlushBySize(T item)
    {
        buffer.AddLast(item);

        if (buffer.Count < batchSize)
            return;

        await FlushBuffer();
    }

    public async Task FlushBuffer()
    {
        if (buffer.Count > 0)
            await Done(buffer.ToArray());
        
        buffer.Clear();
    }

    async Task Done(T[] batch)
    {
        metrics.TrackDone(new BlockItem<T>(batch));

        await done(batch);

        metrics.TrackDoneCompleted(new BlockItem<T>(batch));
    }

    public async Task Send(T item)
    {
        metrics.TrackSend(new BlockItem<T>(item));

        await input.Writer.WriteAsync(item, cancellationToken);
    }

    public async Task Complete()
    {
        input.Writer.Complete();

        await processor;

        await FlushBuffer();
    }

    public int InputCount => metrics.InputCount;
    public int OutputCount => metrics.OutputCount;
    public int WorkingCount => metrics.WorkingCount;
}