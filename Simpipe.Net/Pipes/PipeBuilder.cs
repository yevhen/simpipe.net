namespace Simpipe.Pipes
{
    public class PipeBuilder<T>
    {
        public ActionPipeBuilder<T> Action(Action<T> action) => new(PipeAction<T>.For(action));
        public ActionPipeBuilder<T> Action(Func<T, Task> action) => new(PipeAction<T>.For(action));
        
        public BatchPipeBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, PipeAction<T>.For(action));
        public BatchPipeBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, PipeAction<T>.For(action));
    }
}