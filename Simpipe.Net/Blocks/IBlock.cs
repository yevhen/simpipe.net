namespace Simpipe.Blocks;

public interface IBlock<in T>
{
    int InputCount { get; }
    Task Send(T item);
    Task Complete();
}