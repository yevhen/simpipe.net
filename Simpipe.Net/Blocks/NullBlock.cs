namespace Simpipe.Blocks;

public class NullBlock<T> : IActionBlock<T>
{
    public static NullBlock<T> Instance { get; } = new();

    public Task Send(BlockItem<T> item) => Task.CompletedTask;
    public Task Complete() => Task.CompletedTask;
}