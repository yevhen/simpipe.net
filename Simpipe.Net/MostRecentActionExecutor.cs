using System.Collections.Concurrent;

namespace Youscan.Core.Pipes;

public class MostRecentActionExecutor
{
    readonly BlockingCollection<Func<Task>> buffer = new(1);
    readonly Task executor;

    public MostRecentActionExecutor(CancellationToken cancellation)
    {
        executor = Task.Run(async () =>
        {
            foreach (var action in buffer.GetConsumingEnumerable(cancellation))
                await action();
        },
        cancellation);
    }

    public void Execute(Func<Task> action)
    {
        while (!buffer.TryAdd(action))
            buffer.TryTake(out _);
    }

    public Task Completion => Complete();

    async Task Complete()
    {
        buffer.CompleteAdding();

        try
        {
            await executor;
        }
        catch (OperationCanceledException)
        {
        }
    }
}