using System.Threading.Channels;

namespace Simpipe.Blocks;

public class BatchBlock<T> : IBlock
{
    readonly MetricsTrackingExecutor<T> executor = new();
    readonly Channel<T> input;
    readonly LinkedList<T> buffer = [];
    readonly int batchSize;
    readonly BlockItemAction<T> send;
    readonly BlockItemAction<T> flush;
    readonly BlockItemAction<T> done;
    readonly Task processor;
    readonly CancellationToken cancellationToken;

    public BatchBlock(int capacity, int batchSize, Func<T[], Task> done, CancellationToken cancellationToken = default)
    {
        this.batchSize = batchSize;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<T>(capacity);
        processor = Task.Run(ProcessChannel, cancellationToken);

        this.send = new BlockItemAction<T>(async item => await input.Writer.WriteAsync(item, cancellationToken));
        this.flush = new BlockItemAction<T>(async item => await FlushBySize(item));
        this.done = new BlockItemAction<T>(async items => await done(items));
    }

    async Task ProcessChannel()
    {
        while (await input.Reader.WaitToReadAsync(cancellationToken))
        {
            while (input.Reader.TryRead(out var item))
                await executor.ExecuteAction(new BlockItem<T>(item), flush);
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

    Task Done(T[] batch) => executor.ExecuteDone(new BlockItem<T>(batch), done);

    public Task Send(T item) => executor.ExecuteSend(new BlockItem<T>(item), send);

    public async Task Complete()
    {
        input.Writer.Complete();

        await processor;

        await FlushBuffer();
    }

    public int InputCount => executor.InputCount;
    public int OutputCount => executor.OutputCount;
    public int WorkingCount => executor.WorkingCount;
}