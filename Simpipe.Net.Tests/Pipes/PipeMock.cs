using Simpipe.Blocks;
using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

public static class PipeMock<T>
{
    public static Pipe<T> Create(Action<T> action) =>
        Create(new PipeOptions<T>("id") ,BlockItemAction<T>.Sync(action));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter) =>
        Create(new PipeOptions<T>("id", filter), BlockItemAction<T>.Sync(action));

    public static Pipe<T> Create(Action<T> action, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", null, route), BlockItemAction<T>.Sync(action));

    public static Pipe<T> Create() => Create("id", _ => {});

    public static Pipe<T> Create(string id) => Create(id, _ => {});

    public static Pipe<T> Create(string id, Action<T> action) =>
        Create(new PipeOptions<T>(id), BlockItemAction<T>.Sync(action));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", filter, route), BlockItemAction<T>.Sync(action));

    static Pipe<T> Create(PipeOptions<T> options, BlockItemAction<T> action) =>
        new(options, (done, _) => new BlockMock<T>(action, done));
}

public class BlockMock<T>(BlockItemAction<T> action, BlockItemAction<T> done) : IActionBlock<T>
{
    readonly TaskCompletionSource completionSource = new();

    public async Task Send(BlockItem<T> item)
    {
        await action.Execute(item);
        await done.Execute(item);
    }

    public Task Complete() => completionSource.Task;
    public void SetComplete() => completionSource.TrySetResult();
}

internal static partial class TestingExtensions
{
    public static BlockMock<T> AsBlockMock<T>(this Pipe<T> pipe) => (BlockMock<T>) pipe.Block;
}