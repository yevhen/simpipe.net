namespace Simpipe.Blocks;

public interface IBlock<T>
{
    int InputCount { get; }

    Task Send(BlockItem<T> item);
    Task Complete();

    void SetAction(Func<BlockItem<T>, Task> action)  {}
    void SetDone(Func<BlockItem<T>, Task> done) {}
}

public static class BlockExtensions
{
    public static Task Send<T>(this IBlock<T> block, T item) => block.Send(new BlockItem<T>(item));
    public static Task Send<T>(this IBlock<T> block, T[] items) => block.Send(new BlockItem<T>(items));
}