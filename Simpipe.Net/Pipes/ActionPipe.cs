using Simpipe.Blocks;

namespace Simpipe.Pipes;

public sealed class ActionPipeBuilder<T>(BlockItemAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, Pipe<T>>? route;

    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public ActionPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public ActionPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public ActionPipeBuilder<T> Route(Func<T, Pipe<T>> value)
    {
        route = value;
        return this;
    }

    public ActionPipeBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public ActionPipeBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public ActionPipeBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    public Pipe<T> ToPipe() => new(Options(), done =>
        new ActionBlock<T>(
            boundedCapacity ?? degreeOfParallelism * 2,
            degreeOfParallelism,
            action,
            done,
            null,
            cancellationToken));

    public static implicit operator Pipe<T>(ActionPipeBuilder<T> builder) => builder.ToPipe();
}