using Simpipe.Blocks;

namespace Simpipe.Pipes;

public class Pipe<T>
{
    public static ActionPipeBuilder<T> Action(Action<T> action) => new(BlockItemAction<T>.Sync(action));
    public static ActionPipeBuilder<T> Action(Func<T, Task> action) => new(BlockItemAction<T>.Async(action));

    public static BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockItemAction<T>.BatchSync(action));
    public static BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockItemAction<T>.BatchAsync(action));

    Pipe<T>? next;

    readonly Func<T, bool>? filter;
    readonly List<Func<T, Pipe<T>?>> routes = [];
    readonly TaskCompletionSource completion = new();
    readonly CountingExecutor<T> executor = new();

    public Pipe(PipeOptions<T> options, Func<BlockItemAction<T>, IActionBlockExecutor<T>, IActionBlock<T>> blockFactory)
    {
        Id = options.Id;
        filter = options.Filter;

        var route = options.Route;
        if (route != null)
            routes.Add(route);

        var done = new BlockItemAction<T>(RouteItem);
        Block = blockFactory(done, executor);
    }

    public string Id { get; }
    public IItemCounter ItemCounter => executor;
    internal IActionBlock<T> Block { get; }

    IActionBlock<T> Target(T item) => FilterMatches(item)
        ? Block
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

    Task BlockSend(T item) => Block.Send(item);

    void BlockComplete()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Block.Complete();
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