using Simpipe.Net;

namespace Youscan.Core.Pipes;

public class PipeMock : PipeMock<int>
{
    public PipeMock(string id)
        : base(id)
    {}
}

public class BlockMock<T>(Action<T> action) : IBlock<T>
{
    public Task Send(T item)
    {
        action(item);
        return Task.CompletedTask;
    }

    public Task Complete() => Task.CompletedTask;
}

public class PipeMock<T> : IPipe<T>
{
    public IPipe<T>? Next { get; set; }
    public readonly List<Func<T, IPipe<T>?>> Routes = new();
    readonly TaskCompletionSource completionSource = new();
    public readonly List<T> Received = new();

    public PipeMock(string id)
    {
        Id = id;
    }
        
    public string Id { get; }
    public bool SendExecuted { get; private set; }
    public bool CompleteExecuted { get; private set; }
    public bool SendNextExecuted { get; set; }

    public IBlock<T> Block  => new BlockMock<T>(x =>
    {
        Received.Add(x);
    });
    public IBlock<T> Target(T item) => Block;

    public Task Send(T item)
    {
        Received.Add(item);
        SendExecuted = true;
        return Task.CompletedTask;
    }

    public Task SendNext(T item)
    {
        SendNextExecuted = true;
        return Task.CompletedTask;
    }

    public void Complete()
    {
        CompleteExecuted = true;
    }

    public void ResolveCompletion()
    {
        completionSource.SetResult();
    }

    public Task Completion => completionSource.Task;
       
    public int InputCount { get; set; }
    public int OutputCount { get; set; }
    public int WorkingCount { get; set; }

    public void LinkTo(Func<T, IPipe<T>?> route)
    {
        Routes.Add(route);
    }
}