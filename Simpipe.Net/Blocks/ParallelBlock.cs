using System.Threading.Channels;

namespace Simpipe.Blocks;

public class ParallelBlock<T> : IActionBlock<T>
{
    readonly Channel<BlockItem<T>> input;
    readonly List<IActionBlock<T>> blocks;
    readonly BlockItemAction<T> done;
    readonly CancellationToken cancellationToken;
    readonly Task processor;

    public ParallelBlock(
        int capacity,
        List<IActionBlock<T>> blocks,
        BlockItemAction<T> done,
        CancellationToken cancellationToken = default)
    {
        this.blocks = blocks;
        this.done = done;
        this.cancellationToken = cancellationToken;

        input = Channel.CreateBounded<BlockItem<T>>(capacity);
        processor = Task.Run(ProcessChannel, cancellationToken);
    }

    public async Task Send(BlockItem<T> item) => await input.Writer.WriteAsync(item, cancellationToken);

    public async Task Complete()
    {
        input.Writer.Complete();
        await processor;
        
        var completions = blocks.Select(block => block.Complete());
        await Task.WhenAll(completions);
    }

    async Task ProcessChannel()
    {
        await foreach (var item in input.Reader.ReadAllAsync(cancellationToken))
        {
            await Task.WhenAll(blocks.Select(block => block.Send(item)));
            await done.Execute(item);
        }
    }
}