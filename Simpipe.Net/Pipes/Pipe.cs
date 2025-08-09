using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    Pipe<T>? next;

    readonly Func<T, bool>? filter;
    readonly List<Func<T, Pipe<T>?>> routes = [];
    readonly TaskCompletionSource completion = new();
    readonly IActionBlock<T> block;

    public Pipe(PipeOptions<T> options, Func<BlockItemAction<T>, IActionBlock<T>> blockFactory)
    {
        Id = options.Id;
        filter = options.Filter;

        var route = options.Route;
        if (route != null)
            routes.Add(route);

        var done = new BlockItemAction<T>(RouteItem);
        block = blockFactory(done);
    }

    public string Id { get; }
    public IBlock Block => block;

    IActionBlock<T> Target(T item) => FilterMatches(item)
        ? block
        : RouteTarget(item);

    async Task RouteItem(BlockItem<T> item) => await item.Apply(RouteItem);
    async Task RouteItem(T item) => await RouteTarget(item).Send(item);

    IActionBlock<T> RouteTarget(T item)
    {
        var target = Route(item) ?? next;
        return target == null
            ? NullBlock<T>.Instance
            : target.Target(item);
    }

    Pipe<T>? Route(T item) => routes
        .Select(route => route(item))
        .FirstOrDefault(pipe => pipe != null);

    public async Task Send(T item)
    {
        if (FilterMatches(item))
        {
            await SendThis(item);
            return;
        }

        await SendNext(item);
    }

    bool FilterMatches(T item) => filter == null || filter(item);

    Task SendThis(T item) => BlockSend(item);

    public async Task SendNext(T item)
    {
        if (next != null)
            await next.Send(item);
    }

    public void Complete() => BlockComplete();
    public Task Completion => AwaitCompletion();

    async Task AwaitCompletion()
    {
        try
        {
            await BlockCompletion();
        }
        catch (TaskCanceledException) {}
    }

    public void LinkTo(Func<T, Pipe<T>?> route) => routes.Add(route);
    public void LinkNext(Pipe<T>? next) => this.next = next;

    Task BlockSend(T item) => block.Send(item);

    void BlockComplete()
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

    Task BlockCompletion() => completion.Task;
}