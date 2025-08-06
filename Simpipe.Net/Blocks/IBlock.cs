namespace Simpipe;

public interface IBlock<in T>
{
    int InputCount { get; }
    Task Send(T item);
    Task Complete();
}