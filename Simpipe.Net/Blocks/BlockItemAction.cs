namespace Simpipe.Blocks;

public class BlockItemAction<T>(Func<BlockItem<T>, Task> action)
{
    public static readonly BlockItemAction<T> Noop = new(_ => Task.CompletedTask);
    
    public static BlockItemAction<T> Sync(Action<T> action) => new(item =>
    {
        action(item.GetValue());
        return Task.CompletedTask;
    });

    public static BlockItemAction<T> Async(Func<T, Task> action) => new(item =>
        action(item.GetValue()));

    public static BlockItemAction<T> BatchSync(Action<T[]> action) => new(item =>
    {
        action(item.GetArray());
        return Task.CompletedTask;
    });

    public static BlockItemAction<T> BatchAsync(Func<T[], Task> action) => new(item =>
        action(item.GetArray()));

    public async Task Execute(BlockItem<T> item) => await action(item);
}