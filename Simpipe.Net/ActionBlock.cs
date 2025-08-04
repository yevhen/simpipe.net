using System.Threading.Channels;

namespace Simpipe.Channels;

public class ActionBlock<T>
{
    private readonly ChannelReader<T> _reader;
    private readonly Func<T, Task>? _asyncAction;
    private readonly Action<T>? _syncAction;
    private readonly Action<T> _done;
    private readonly int _parallelism;

    // Constructor for sync action without parallelism (original)
    public ActionBlock(ChannelReader<T> reader, Action<T> action, Action<T> done)
        : this(reader, action, done, parallelism: 1)
    {
    }

    // Constructor for sync action with parallelism
    public ActionBlock(ChannelReader<T> reader, Action<T> action, Action<T> done, int parallelism)
    {
        _reader = reader;
        _syncAction = action;
        _done = done;
        _parallelism = parallelism;
    }

    // Constructor for async action with parallelism
    public ActionBlock(ChannelReader<T> reader, Func<T, Task> action, int parallelism)
    {
        _reader = reader;
        _asyncAction = action;
        _done = _ => { }; // No-op done callback for now
        _parallelism = parallelism;
    }

    public async Task RunAsync()
    {
        if (_parallelism == 1)
        {
            // Sequential processing
            await foreach (var item in _reader.ReadAllAsync())
            {
                await ProcessItem(item);
            }
        }
        else
        {
            // Parallel processing
            using var semaphore = new SemaphoreSlim(_parallelism, _parallelism);
            var tasks = new List<Task>();
            
            await foreach (var item in _reader.ReadAllAsync())
            {
                await semaphore.WaitAsync();
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessItem(item);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessItem(T item)
    {
        if (_asyncAction != null)
        {
            await _asyncAction(item);
        }
        else
        {
            _syncAction!(item);
        }
        _done(item);
    }
}