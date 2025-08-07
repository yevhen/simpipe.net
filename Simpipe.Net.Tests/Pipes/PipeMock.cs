using Simpipe.Blocks;
using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

public static class PipeMock<T>
{
    public static Pipe<T> Create(Action<T> action) =>
        Create(new PipeOptions<T>("id", BlockItemAction<T>.Sync(action), null, null));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter) =>
        Create(new PipeOptions<T>("id", BlockItemAction<T>.Sync(action), filter, null));

    public static Pipe<T> Create(Action<T> action, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", BlockItemAction<T>.Sync(action), null, route));

    public static Pipe<T> Create() => Create("id", _ => { });

    public static Pipe<T> Create(string id) => Create(id, _ => { });

    public static Pipe<T> Create(string id, Action<T> action) =>
        Create(new PipeOptions<T>(id, BlockItemAction<T>.Sync(action), null, null));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", BlockItemAction<T>.Sync(action), filter, route));

    static Pipe<T> Create(PipeOptions<T> options) =>
        new(options, new BlockMock<T>());
}

public class BlockMock<T> : IBlock<T>
{
    Func<BlockItem<T>, Task> execute = _ => Task.CompletedTask;
    Func<BlockItem<T>, Task> done = _ => Task.CompletedTask;

    readonly TaskCompletionSource completionSource = new();

    public int InputCount => 0;

    public async Task Send(BlockItem<T> item)
    {
        await execute(item);
        await done(item);
    }

    public Task Complete() => completionSource.Task;
    public void SetComplete() => completionSource.TrySetResult();

    public void SetAction(Func<BlockItem<T>, Task> action) => execute = action;
    public void SetDone(Func<BlockItem<T>, Task> done) => this.done = done;
}

internal static partial class TestingExtensions
{
    public static BlockMock<T> AsBlockMock<T>(this Pipe<T> pipe) => (BlockMock<T>) pipe.Block;
}