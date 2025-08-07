namespace Simpipe.Blocks;

public interface ICounter<T>
{
    int InputCount { get; }
    int OutputCount { get; }
    int WorkingCount { get; }
}

public class CounterBlock<T>(IActionBlock<T> inner) : IActionBlock<T>
{
    public int InputCount => 0;
    public int OutputCount => 0;
    public int WorkingCount => 0;

    public Task Send(BlockItem<T> item) => inner.Send(item);
    public Task Complete() => inner.Complete();
}