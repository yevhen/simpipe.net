namespace Simpipe.Pipes
{
    public class PipeBuilder<T>
    {
        public ActionPipeOptions<T> Action(Action<T> action) => new(PipeAction<T>.For(action));
        public ActionPipeOptions<T> Action(Func<T, Task> action) => new(PipeAction<T>.For(action));
        
        public BatchPipeOptions<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, PipeAction<T>.For(action));
        public BatchPipeOptions<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, PipeAction<T>.For(action));
    }
}