namespace Simpipe.Blocks;

public class FilterBlock<T>(IActionBlock<T> inner, Func<T, bool> filter, BlockItemAction<T> done) : IActionBlock<T>
{
    public int InputCount => inner.InputCount;
    public int OutputCount => inner.OutputCount;
    public int WorkingCount => inner.WorkingCount;

    public async Task Send(BlockItem<T> item)
    {
        if (!filter(item))
        {
            await done.Execute(item);
            return;
        }

        await inner.Send(item);
    }

    public Task Complete() => inner.Complete();
}