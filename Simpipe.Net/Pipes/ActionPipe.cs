namespace Simpipe.Pipes;

public sealed class ActionPipeOptions<T>(PipeAction<T> action) : PipeOptions<T>(action)
{
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public ActionPipeOptions<T> Id(string value)
    {
        id = value;
        return this;
    }

    public ActionPipeOptions<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public ActionPipeOptions<T> Route(Func<T, IPipe<T>> value)
    {
        route = value;
        return this;
    }

    public ActionPipeOptions<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public ActionPipeOptions<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public ActionPipeOptions<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    public Pipe<T> ToPipe() => ActionPipe<T>.Create(this);
    public static implicit operator Pipe<T>(ActionPipeOptions<T> options) => options.ToPipe();

    public int? BoundedCapacity() => boundedCapacity;
    public CancellationToken CancellationToken() => cancellationToken;
    public int DegreeOfParallelism() => degreeOfParallelism;
}

public abstract class ActionPipe<T>
{
    public static Pipe<T> Create(ActionPipeOptions<T> options) => new(options, (execute, route) =>
        new ActionBlock<T>(
            options.BoundedCapacity() ?? options.DegreeOfParallelism() * 2,
            options.DegreeOfParallelism(),
            item => execute(new PipeItem<T>(item)),
            route,
            options.CancellationToken()));
}