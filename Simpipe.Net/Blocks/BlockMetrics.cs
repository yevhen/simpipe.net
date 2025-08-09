namespace Simpipe.Blocks;

internal class BlockMetrics<T>
{
    volatile int inputCount;
    volatile int outputCount;
    volatile int workingCount;

    public int InputCount => inputCount;
    public int OutputCount => outputCount;
    public int WorkingCount => workingCount;

    public void TrackSend(BlockItem<T> item)
    {
        Interlocked.Add(ref inputCount, item.Size);
    }

    public void TrackExecute(BlockItem<T> item)
    {
        Interlocked.Add(ref inputCount, -item.Size);
        Interlocked.Add(ref workingCount, item.Size);
    }

    public void TrackDone(BlockItem<T> item)
    {
        Interlocked.Add(ref workingCount, -item.Size);
        Interlocked.Add(ref outputCount, item.Size);
    }

    public void TrackGone(BlockItem<T> item)
    {
        Interlocked.Add(ref outputCount, -item.Size);
    }
}