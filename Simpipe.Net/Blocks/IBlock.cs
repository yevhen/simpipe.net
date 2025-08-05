namespace Simpipe.Net;

public interface IBlock<in T>
{
    Task Send(T item);
    Task Complete();
}