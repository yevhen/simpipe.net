using System.Threading.Channels;

namespace Simpipe.Channels;

public class ActionBlock<T>(ChannelReader<T> reader, Action<T> action, Action<T> done)
{
    public async Task RunAsync()
    {
        await foreach (var item in reader.ReadAllAsync())
        {
            action(item);
            done(item);
        }
    }
}