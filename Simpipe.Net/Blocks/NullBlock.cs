namespace Simpipe.Blocks;

public class NullBlock<T> : IBlock<T>
{
    public static NullBlock<T> Instance { get; } = new();

    public int InputCount => 0;
    public Task Send(BlockItem<T> item) => Task.CompletedTask;
    public Task Complete() => Task.CompletedTask;
}