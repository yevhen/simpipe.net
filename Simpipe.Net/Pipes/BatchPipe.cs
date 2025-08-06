namespace Simpipe.Pipes;

public sealed class BatchPipeOptions<T>(int batchSize, PipeAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, IPipe<T>>? route;

    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

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

    public BatchPipeOptions<T> Route(Func<T, IPipe<T>> value)
    {
        route = value;
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

    PipeOptions<T> Options() => new(id, action, filter, route);

    public Pipe<T> ToPipe() => new(Options(), (execute, route) =>
        new BatchActionBlock<T>(
            boundedCapacity ?? batchSize,
            batchSize,
            batchTriggerPeriod != TimeSpan.Zero ? batchTriggerPeriod : Timeout.InfiniteTimeSpan,
            degreeOfParallelism,
            items => execute(new PipeItem<T>(items)),
            route,
            cancellationToken));

    public static implicit operator Pipe<T>(BatchPipeOptions<T> options) => options.ToPipe();
}
