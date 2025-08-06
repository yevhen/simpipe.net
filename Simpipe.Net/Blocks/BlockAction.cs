using Simpipe.Blocks;

namespace Simpipe.Pipes;

public class BlockAction<T>(Func<BlockItem<T>, Task> action)
{
    public static BlockAction<T> For(Action<T> action) => new(item =>
    {
        action(item.GetValue());
        return Task.CompletedTask;
    });

    public static BlockAction<T> For(Func<T, Task> action) => new(async item =>
    {
        await action(item.GetValue());
    });

    public static BlockAction<T> For(Action<T[]> action) => new(item =>
    {
        action(item.GetArray());
        return Task.CompletedTask;
    });

    public static BlockAction<T> For(Func<T[], Task> action) => new(item => action(item.GetArray()));
    public static BlockAction<T> For(Func<BlockItem<T>, Task> action) => new(action);

    public async Task Execute(BlockItem<T> item) => await action(item);
}