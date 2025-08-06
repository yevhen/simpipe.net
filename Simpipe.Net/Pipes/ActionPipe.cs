namespace Simpipe.Pipes;

public sealed class ActionPipeOptions<T>(PipeAction<T> action) : PipeOptions<T>
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

    public ActionPipe<T> ToPipe() => ActionPipe<T>.Create(this);
    public static implicit operator Pipe<T>(ActionPipeOptions<T> options) => options.ToPipe();

    public PipeAction<T> Action() => action;
    public int? BoundedCapacity() => boundedCapacity;
    public CancellationToken CancellationToken() => cancellationToken;
    public int DegreeOfParallelism() => degreeOfParallelism;
}

public class ActionPipe<T> : Pipe<T>
{
    readonly ActionBlock<T> block;

    ActionPipe(ActionPipeOptions<T> options) : base(options, options.Action())
    {
        var boundedCapacity = options.BoundedCapacity() ?? options.DegreeOfParallelism() * 2;

        block = new ActionBlock<T>(boundedCapacity,
            options.DegreeOfParallelism(),
            blockAction.Execute,
            RouteItem,
            options.CancellationToken());

        Block = block;
    }

    public static ActionPipe<T> Create(ActionPipeOptions<T> options)
    {
        return new ActionPipe<T>(options);
    }

    public override IBlock<T> Block { get; }
}