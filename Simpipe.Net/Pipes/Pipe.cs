using Simpipe.Blocks;

namespace Simpipe.Pipes;

public class Pipe<T>
{
    public static ActionPipeBuilder<T> Action(Action<T> action) => new(BlockAction<T>.For(action));
    public static ActionPipeBuilder<T> Action(Func<T, Task> action) => new(BlockAction<T>.For(action));

    public static BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockAction<T>.For(action));
    public static BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockAction<T>.For(action));

    public string Id { get; }
    public Pipe<T>? Next { get; private set; }

    readonly List<Func<T, Pipe<T>?>> routes = [];
    readonly Func<T, bool>? filter;
    readonly BlockAction<T> action;
    readonly TaskCompletionSource completion = new();

    volatile int workingCount;
    volatile int outputCount;

    public Pipe(PipeOptions<T> options, Func<Func<BlockItem<T>, Task>, Func<T, Task>, IBlock<T>> blockFactory)
    {
        Id = options.Id;
        filter = options.Filter;
        action = options.Action;

        var route = options.Route;
        if (route != null)
            routes.Add(route);

        var blockAction = BlockAction<T>.For(ExecuteAction);
        Block = blockFactory(blockAction.Execute, RouteItem);
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