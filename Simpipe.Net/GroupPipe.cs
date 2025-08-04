using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes;

public class GroupPipe<T> : Pipe<T>
{
    readonly BufferBlock<T> block;
    readonly List<IPipe<T>> pipes = new();

    public GroupPipe(GroupPipeOptions<T> options) : base(options)
    {
        block = new BufferBlock<T>(new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = options.BoundedCapacity() ?? -1,
            CancellationToken = options.CancellationToken(),
            EnsureOrdered = false,
            MaxDegreeOfParallelism = options.DegreeOfParallelism(),
        });
    }

    public int BufferCount => block.Count;
    
    public void Add(IPipe<T> child, Func<T, IPipe<T>, bool>? predicate = null)
    {
        pipes.Add(child);
        child.Next = Next;

        if (predicate == null)
        {
            block.LinkTo(child.Block);
            return;
        }

        block.LinkTo(child.Block, x => predicate(x, child));
    }

    IPipe<T>? next; 

    public override IPipe<T>? Next
    {
        get => next;
        set
        {
            next = value;
            pipes.ForEach(x => x.Next = value!);
        }
    }

    public override void LinkTo(Func<T, IPipe<T>?> route) => 
        pipes.ForEach(x => x.LinkTo(route));

    public override int InputCount => pipes.Sum(x => x.InputCount);
    public override int OutputCount => pipes.Sum(x => x.OutputCount);
    public override int WorkingCount => pipes.Sum(x => x.WorkingCount);

    protected override async Task BlockSend(T item) => await block.SendAsync(item);
    protected override void BlockComplete() => block.Complete();

    protected override async Task BlockCompletion()
    {
        await block.Completion;
        
        pipes.ForEach(pipe => pipe.Complete());

        await Task.WhenAll(pipes.Select(pipe => pipe.Completion));
    }

    public override ITargetBlock<T> Block => block;
}