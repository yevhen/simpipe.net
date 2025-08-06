namespace Simpipe.Pipes;

public class BlockAction<T>(Func<BlockItem<T>, Task> action)
{
    public Func<BlockItem<T>, Task> InnerAction => action;

    public static BlockAction<T> Noop() => new(_ => Task.CompletedTask);

    public static BlockAction<T> For(Action<T> action) => new(item =>
    {
        action(item);
        return Task.CompletedTask;
    });

    public static BlockAction<T> For(Func<T, Task> action) => new(async item =>
    {
        await action(item);
    });

    public static BlockAction<T> For(Action<T[]> action) => new(item =>
    {
        action(item);
        return Task.CompletedTask;
    });

    public static BlockAction<T> For(Func<T[], Task> action) => new(item => action(item));
    public static BlockAction<T> For(Func<BlockItem<T>, Task> action) => new(action);

    public async Task Execute(BlockItem<T> item) => await action(item);
}