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

    public Pipe<T> ToPipe() => new(this, (execute, route) =>
        new BatchActionBlock<T>(
            BoundedCapacity() ?? BatchSize(),
            BatchSize(),
            BatchTriggerPeriod() != TimeSpan.Zero ? BatchTriggerPeriod() : Timeout.InfiniteTimeSpan,
            DegreeOfParallelism(),
            items => execute(new PipeItem<T>(items)),
            route,
            CancellationToken()));

    public static implicit operator Pipe<T>(BatchPipeOptions<T> options) => options.ToPipe();

    public int? BoundedCapacity() => boundedCapacity;
    public CancellationToken CancellationToken() => cancellationToken;
    public int DegreeOfParallelism() => degreeOfParallelism;
}
