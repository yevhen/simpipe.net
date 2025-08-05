using System.Collections;

namespace Simpipe.Pipes
{
    public class Pipeline<T> : IEnumerable<IPipe<T>>
    {
        IPipe<T>? head;
        IPipe<T>? last;

        readonly TaskCompletionSource completion = new();

        readonly Dictionary<string, IPipe<T>> pipesById = new();
        readonly List<IPipe<T>> pipes = new();

        readonly Func<T, IPipe<T>?>? defaultRoute;

        public Pipeline(Func<T, IPipe<T>?>? defaultRoute = null) => 
            this.defaultRoute = defaultRoute;

        public void Add(IPipe<T> pipe)
        {
            if (pipesById.ContainsKey(pipe.Id))
                throw new Exception($"The pipe with id {pipe.Id} already exists");

            pipesById.Add(pipe.Id, pipe);
            pipes.Add(pipe);

            head ??= pipe;
            Link(pipe);

            last = pipe;
        }

        void Link(IPipe<T> pipe)
        {
            if (defaultRoute != null)
                pipe.LinkTo(defaultRoute);
            
            last?.LinkTo(pipe);
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
            if (id == null)
                throw new Exception($"The pipe with id '{id}' does not exist");
            
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

        public IEnumerator<IPipe<T>> GetEnumerator() => pipes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class PipeNotFoundException: Exception
    {
        public PipeNotFoundException(string message) : base(message)
        {
        }
    }
}