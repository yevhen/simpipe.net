namespace Simpipe.Net;

public class NullBlock<T> : IBlock<T>
{
    public static NullBlock<T> Instance { get; } = new();

    public Task Send(T item) => Task.CompletedTask;
    public Task Complete() => Task.CompletedTask;
}