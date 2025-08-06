using Simpipe.Blocks;

namespace Simpipe.Pipes;

public class Pipe<T>
{
    public static ActionPipeBuilder<T> Action(Action<T> action) => new(BlockAction<T>.Sync(action));
    public static ActionPipeBuilder<T> Action(Func<T, Task> action) => new(BlockAction<T>.Async(action));

    public static BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockAction<T>.BatchSync(action));
    public static BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockAction<T>.BatchAsync(action));

    public string Id { get; }
    public Pipe<T>? Next { get; private set; }

    readonly List<Func<T, Pipe<T>?>> routes = [];
    readonly Func<T, bool>? filter;
    readonly BlockAction<T> action;
    readonly TaskCompletionSource completion = new();

    volatile int workingCount;
    volatile int outputCount;

    public Pipe(PipeOptions<T> options, IBlock<T> block)
    {
        Id = options.Id;
        filter = options.Filter;
        action = options.Action;

        var route = options.Route;
        if (route != null)
            routes.Add(route);

        Block = block;

        block.SetAction(ExecuteAction);
        block.SetDone(RouteItem);
    }

    public IBlock<T> Block { get; }

    async Task ExecuteAction(BlockItem<T> item)
    {
        Interlocked.Add(ref workingCount, item.Size);

        await action.Execute(item);

        Interlocked.Add(ref workingCount, -item.Size);
    }

    IBlock<T> Target(T item) => FilterMatches(item)
        ? Block
        : RouteTarget(item);

    async Task RouteItem(BlockItem<T> item) => await item.Apply(RouteItem);

    async Task RouteItem(T item)
    {
        Interlocked.Increment(ref outputCount);

        await RouteTarget(item).Send(item);

        Interlocked.Decrement(ref outputCount);
    }

    IBlock<T> RouteTarget(T item)
    {
        var target = Route(item) ?? Next;
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
        if (Next != null)
            await Next.Send(item);
    }

    public int InputCount => Block.InputCount;
    public int OutputCount => outputCount;
    public int WorkingCount => workingCount;

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
    public void LinkNext(Pipe<T>? next) => Next = next;

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