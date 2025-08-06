namespace Simpipe.Pipes;

public sealed class BatchPipeOptions<T>(int batchSize, PipeAction<T> action) : PipeOptions<T>
{
    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public int BatchSize() => batchSize;
    public TimeSpan BatchTriggerPeriod() => batchTriggerPeriod;

    public BatchPipeOptions<T> Id(string value)
    {
        id = value;
        return this;
    }

    public BatchPipeOptions<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public BatchPipeOptions<T> BatchTriggerPeriod(TimeSpan value)
    {
        batchTriggerPeriod = value;
        return this;
    }

    public BatchPipeOptions<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public BatchPipeOptions<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public BatchPipeOptions<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    public BatchPipe<T> ToPipe() => new(this);
    public static implicit operator Pipe<T>(BatchPipeOptions<T> options) => options.ToPipe();

    public PipeAction<T> Action() => action;
    public int? BoundedCapacity() => boundedCapacity;
    public CancellationToken CancellationToken() => cancellationToken;
    public int DegreeOfParallelism() => degreeOfParallelism;
}

public class BatchPipe<T> : Pipe<T>
{
    readonly BatchActionBlock<T> block;
    readonly TaskCompletionSource completion = new();

    public BatchPipe(BatchPipeOptions<T> options) : base(options, options.Action())
    {
        block = new BatchActionBlock<T>(
            options.BoundedCapacity() ?? options.BatchSize(),
            options.BatchSize(),
            options.BatchTriggerPeriod() != TimeSpan.Zero ? options.BatchTriggerPeriod() : Timeout.InfiniteTimeSpan,
            options.DegreeOfParallelism(),
            Execute,
            RouteItem,
            options.CancellationToken());

        Block = block;
    }

    public override int InputCount => block.InputCount;

    async Task<T[]> Execute(T[] item)
    {
        await ExecuteAction(item);
        return item;
    }

    async Task ExecuteAction(T[] item) => await blockAction.Execute(item);

    protected override Task BlockSend(T item) => block.Send(item);

    protected override void BlockComplete()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await block.Complete();
                completion.TrySetResult();
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
        });
    }

    protected override Task BlockCompletion() => completion.Task;

    public override IBlock<T> Block { get; }
}