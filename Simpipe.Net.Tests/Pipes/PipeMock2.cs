using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

public static class PipeMock2<T>
{
    public static Pipe<T> Create(Action<T> action) =>
        new(new PipeOptions<T>("id", PipeAction<T>.For(action), null, null),
            (execute, done) => new BlockMock2<T>(execute, done));

    public static Pipe<T> Create(PipeOptions<T> options) =>
        new(options, (execute, done) => new BlockMock2<T>(execute, done));

    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), filter, null));
    
    public static Pipe<T> Create(Action<T> action, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), null, route));
        
    public static Pipe<T> Create(string id, Action<T> action) =>
        Create(new PipeOptions<T>(id, PipeAction<T>.For(action), null, null));
    
    public static Pipe<T> Create(Action<T> action, Func<T, bool> filter, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), filter, route));
        
    public static Pipe<T> Create(Func<T, Task> action) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), null, null));
        
    public static Pipe<T> Create(Func<T, Task> action, Func<T, bool> filter) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), filter, null));
        
    public static Pipe<T> Create(Func<T, Task> action, Func<T, Pipe<T>?> route) =>
        Create(new PipeOptions<T>("id", PipeAction<T>.For(action), null, route));
        
    public static Pipe<T> Create(string id, Func<T, Task> asyncAction) =>
        Create(new PipeOptions<T>(id, PipeAction<T>.For(asyncAction), null, null));
}

public class BlockMock2<T>(Func<PipeItem<T>, Task> execute, Func<T, Task> done) : IBlock<T>
{
    public int InputCount => 0;

    public Task Send(T item)
    {
        var pipeItem = new PipeItem<T>(item);
        return execute(pipeItem).ContinueWith(_ => done(item));
    }

    public Task Complete() => Task.CompletedTask;
}