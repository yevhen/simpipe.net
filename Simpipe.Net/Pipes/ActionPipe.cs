namespace Simpipe.Pipes;

public sealed class ActionPipeBuilder<T>(PipeAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, IPipe<T>>? route;

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

    public ActionPipeBuilder<T> Route(Func<T, IPipe<T>> value)
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

    PipeOptions<T> Options() => new(id, action, filter, route);

    public Pipe<T> ToPipe() => new(Options(), (execute, router) =>
        new ActionBlock<T>(
            boundedCapacity ?? degreeOfParallelism * 2,
            degreeOfParallelism,
            item => execute(new PipeItem<T>(item)),
            router,
            cancellationToken));

    public static implicit operator Pipe<T>(ActionPipeBuilder<T> builder) => builder.ToPipe();
}