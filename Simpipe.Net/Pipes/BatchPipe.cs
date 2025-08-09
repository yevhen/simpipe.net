using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    public static BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockItemAction<T>.BatchSync(action));
    public static BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockItemAction<T>.BatchAsync(action));
}

public sealed class BatchPipeBuilder<T>(int batchSize, BlockItemAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, Pipe<T>>? route;

    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public BatchPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public BatchPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public BatchPipeBuilder<T> Route(Func<T, Pipe<T>> value)
    {
        route = value;
        return this;
    }

    public BatchPipeBuilder<T> BatchTriggerPeriod(TimeSpan value)
    {
        batchTriggerPeriod = value;
        return this;
    }

    public BatchPipeBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public BatchPipeBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public BatchPipeBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    public Pipe<T> ToPipe() => new(Options(), done =>
        new BatchActionBlock<T>(
            boundedCapacity ?? batchSize,
            batchSize,
            batchTriggerPeriod != TimeSpan.Zero ? batchTriggerPeriod : Timeout.InfiniteTimeSpan,
            degreeOfParallelism,
            action,
            done,
            cancellationToken));

    public static implicit operator Pipe<T>(BatchPipeBuilder<T> builder) => builder.ToPipe();
}
