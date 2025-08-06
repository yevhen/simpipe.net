namespace Simpipe.Pipes;

public sealed class BatchPipeOptions<T>(int batchSize, PipeAction<T> action) : PipeOptions<T>(action)
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

    public Pipe<T> ToPipe() => BatchPipe<T>.Create(this);
    public static implicit operator Pipe<T>(BatchPipeOptions<T> options) => options.ToPipe();

    public int? BoundedCapacity() => boundedCapacity;
    public CancellationToken CancellationToken() => cancellationToken;
    public int DegreeOfParallelism() => degreeOfParallelism;
}

public abstract class BatchPipe<T>
{
    public static Pipe<T> Create(BatchPipeOptions<T> options)
    {
        var pipe = new Pipe<T>(options, (execute, route) =>
            new BatchActionBlock<T>(
                options.BoundedCapacity() ?? options.BatchSize(),
                options.BatchSize(),
                options.BatchTriggerPeriod() != TimeSpan.Zero ? options.BatchTriggerPeriod() : Timeout.InfiniteTimeSpan,
                options.DegreeOfParallelism(),
                items => execute(new PipeItem<T>(items)),
                route,
                options.CancellationToken()));

        return pipe;
    }
}