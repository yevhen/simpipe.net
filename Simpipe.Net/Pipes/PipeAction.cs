namespace Simpipe.Pipes;

public class PipeAction<T>(Func<PipeItem<T>, Task> action)
{
    public static PipeAction<T> For(Action<T> action) => new(item =>
    {
        action(item);
        return Task.CompletedTask;
    });

    public static PipeAction<T> For(Func<T, Task> action) => new(async item =>
    {
        await action(item);
    });

    public static PipeAction<T> For(Action<T[]> action) => new(item =>
    {
        action(item);
        return Task.CompletedTask;
    });

    public static PipeAction<T> For(Func<T[], Task> action) => new(item => action(item));
    public static PipeAction<T> For(Func<PipeItem<T>, Task> action) => new(action);

    public async Task Execute(PipeItem<T> item) => await action(item);
}