namespace Simpipe.Pipes
{
    public class PipeAction<T>
    {
        public static PipeAction<T> None() => For((T _) => {});

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

        PipeAction(Func<PipeItem<T>, Task> action) => 
            Action = action;

        Func<PipeItem<T>, Task> Action { get; init; }

        public async Task Execute(T item) => await Action(new PipeItem<T>(item));
        public async Task Execute(T[] items) => await Action(new PipeItem<T>(items));
        public async Task Execute(PipeItem<T> item) => await Action(item);
    }
}