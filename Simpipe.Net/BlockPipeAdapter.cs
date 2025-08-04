using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes;

public class BlockPipeAdapter<T> : Pipe<T>
{
    readonly ITargetBlock<T> inner;
    readonly TransformBlock<T, T>? transform;

    public BlockPipeAdapter(ITargetBlock<T> inner)
        : this(new PipeOptions<T>(), inner)
    {}

    public BlockPipeAdapter(PipeOptions<T> options, ITargetBlock<T> inner)
        : base(options)
    {
        this.inner = inner;
        transform = inner as TransformBlock<T, T>;
    }

    public override int InputCount => transform?.InputCount ?? 0;
    public override int OutputCount => transform?.OutputCount ?? 0;

    protected override Task BlockSend(T item) => inner.SendAsync(item);

    protected override void BlockComplete() => inner.Complete();
    protected override Task BlockCompletion() => inner.Completion;

    public override ITargetBlock<T> Block => inner;
}