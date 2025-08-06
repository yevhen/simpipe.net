using Simpipe.Blocks;
using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

public static class PipeMock<T>
{
    public static Pipe<T> Create(Action<T> action) =>
        new(new PipeOptions<T>("id", BlockAction<T>.For(action), null, null),
            (execute, done) => new BlockMock<T>(execute, done));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter) =>
        Create(new PipeOptions<T>("id", BlockAction<T>.For(action), filter, null));

    public static Pipe<T> Create(Action<T> action, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", BlockAction<T>.For(action), null, route));

    public static Pipe<T> Create() => Create("id", _ => { });

    public static Pipe<T> Create(string id) => Create(id, _ => { });

    public static Pipe<T> Create(string id, Action<T> action) =>
        Create(new PipeOptions<T>(id, BlockAction<T>.For(action), null, null));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", BlockAction<T>.For(action), filter, route));

    static Pipe<T> Create(PipeOptions<T> options) =>
        new(options, (execute, done) => new BlockMock<T>(execute, done));
}

public class BlockMock<T>(Func<BlockItem<T>, Task> execute, Func<T, Task> done) : IBlock<T>
{
    readonly TaskCompletionSource completionSource = new();

    public int InputCount => 0;

    public async Task Send(T item)
    {
        await execute(new BlockItem<T>(item));
        await done(item);
    }

    public Task Complete() => completionSource.Task;
    public void SetComplete() => completionSource.TrySetResult();
}

internal static partial class TestingExtensions
{
    public static BlockMock<T> AsBlockMock<T>(this Pipe<T> pipe) => (BlockMock<T>) pipe.Block;
}