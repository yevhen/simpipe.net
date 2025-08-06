using System.Collections;

namespace Simpipe.Pipes;

public class Pipeline<T>(Func<T, Pipe<T>?>? defaultRoute = null) : IEnumerable<Pipe<T>>
{
    Pipe<T>? head;
    Pipe<T>? last;

    readonly TaskCompletionSource completion = new();

    readonly Dictionary<string, Pipe<T>> pipesById = new();
    readonly List<Pipe<T>> pipes = [];

    public void Add(Pipe<T> pipe)
    {
        if (!pipesById.TryAdd(pipe.Id, pipe))
            throw new Exception($"The pipe with id {pipe.Id} already exists");

        pipes.Add(pipe);

        head ??= pipe;
        Link(pipe);

        last = pipe;
    }

    void Link(Pipe<T> pipe)
    {
        if (defaultRoute != null)
            pipe.LinkTo(defaultRoute);

        last?.LinkNext(pipe);
    }

    public async Task Send(T item, string? id = null)
    {
        if (id == null)
        {
            await head!.Send(item);
            return;
        }

        if (!pipesById.TryGetValue(id, out var target))
            throw new PipeNotFoundException($"The pipe with id '{id}' does not exist");

        await target.Send(item);
    }

    public async Task SendNext(T item, string id)
    {
        if (!pipesById.TryGetValue(id, out var source))
            throw new Exception($"The pipe with id '{id}' does not exist");

        await source.SendNext(item);
    }

    public async Task Complete()
    {
        foreach (var pipe in pipes)
        {
            pipe.Complete();
            await pipe.Completion;
        }

        completion.SetResult();
    }

    public Task Completion => completion.Task;

    public IEnumerator<Pipe<T>> GetEnumerator() => pipes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class PipeNotFoundException(string message) : Exception(message);