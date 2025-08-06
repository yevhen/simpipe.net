namespace Simpipe.Blocks;

public class BlockAction<T>(Func<BlockItem<T>, Task> action)
{
    public static BlockAction<T> Sync(Action<T> action) => new(item =>
    {
        action(item.GetValue());
        return Task.CompletedTask;
    });

    public static BlockAction<T> Async(Func<T, Task> action) => new(item =>
        action(item.GetValue()));

    public static BlockAction<T> BatchSync(Action<T[]> action) => new(item =>
    {
        action(item.GetArray());
        return Task.CompletedTask;
    });

    public static BlockAction<T> BatchAsync(Func<T[], Task> action) => new(item =>
        action(item.GetArray()));

    public async Task Execute(BlockItem<T> item) => await action(item);
}