using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    public static ForkPipeBuilder<T> Fork(params IParallelBlockBuilder<T>[] blocks) => new(blocks);
}

public class ForkPipeBuilder<T>(params IParallelBlockBuilder<T>[] blocks)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, Pipe<T>>? route;
    Action<T> join = _ => {};

    public ForkPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public ForkPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public ForkPipeBuilder<T> Route(Func<T, Pipe<T>> value)
    {
        route = value;
        return this;
    }

    public ForkPipeBuilder<T> Join(Action<T> value)
    {
        join = value ?? throw new ArgumentNullException(nameof(value), "Join action cannot be null.");
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    public Pipe<T> ToPipe() => new(Options(), done =>
    {
        if (blocks.Length == 0)
            throw new ArgumentException("At least one block must be provided.", nameof(blocks));

        return new ParallelBlock<T>(
            blocks.Length,
            new BlockItemAction<T>(async item =>
            {
                join(item);
                await done.Execute(item);
            }),
            innerDone => blocks.ToDictionary(b => b.Id, b => b.ToBlock(innerDone)));
    });

    public static implicit operator Pipe<T>(ForkPipeBuilder<T> builder) => builder.ToPipe();
}

public static class Parallel<T>
{
    public static ParallelActionBlockBuilder<T> Action(Action<T> action) => new(BlockItemAction<T>.Sync(action));
    public static ParallelActionBlockBuilder<T> Action(Func<T, Task> action) => new(BlockItemAction<T>.Async(action));

    public static ParallelBatchActionBlockBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockItemAction<T>.BatchSync(action));
    public static ParallelBatchActionBlockBuilder<T> Batch(int batchSize, Func<T[], Task> action) => new(batchSize, BlockItemAction<T>.BatchAsync(action));
}

public interface IParallelBlockBuilder<T>
{
    string Id { get; }
    IActionBlock<T> ToBlock(BlockItemAction<T> done);
}

public class ParallelActionBlockBuilder<T>(BlockItemAction<T> action) : IParallelBlockBuilder<T>
{
    string id = "block-id";
    Func<T, bool>? filter;

    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public ParallelActionBlockBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public ParallelActionBlockBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public ParallelActionBlockBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public ParallelActionBlockBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public ParallelActionBlockBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    string IParallelBlockBuilder<T>.Id => id;

    IActionBlock<T> IParallelBlockBuilder<T>.ToBlock(BlockItemAction<T> done)
    {
        var block = CreateActionBlock(done);
        return CreateFilterBlock(block, done);
    }

    IActionBlock<T> CreateFilterBlock(IActionBlock<T> block, BlockItemAction<T> done) =>
        filter is null ? block : new FilterBlock<T>(block, filter, done);

    ActionBlock<T> CreateActionBlock(BlockItemAction<T> done) => new(
        boundedCapacity ?? degreeOfParallelism * 2,
        degreeOfParallelism,
        action,
        done,
        cancellationToken);
}

public sealed class ParallelBatchActionBlockBuilder<T>(int batchSize, BlockItemAction<T> action) : IParallelBlockBuilder<T>
{
    string id = "block-id";
    Func<T, bool>? filter;

    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    public ParallelBatchActionBlockBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    public ParallelBatchActionBlockBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    public ParallelBatchActionBlockBuilder<T> BatchTriggerPeriod(TimeSpan value)
    {
        batchTriggerPeriod = value;
        return this;
    }

    public ParallelBatchActionBlockBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    public ParallelBatchActionBlockBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    public ParallelBatchActionBlockBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    string IParallelBlockBuilder<T>.Id => id;

    IActionBlock<T> IParallelBlockBuilder<T>.ToBlock(BlockItemAction<T> done)
    {
        var block = CreateBatchActionBlock(done);
        return CreateFilterBlock(block, done);
    }

    IActionBlock<T> CreateFilterBlock(IActionBlock<T> block, BlockItemAction<T> done) =>
        filter is null ? block : new FilterBlock<T>(block, filter, done);

    IActionBlock<T> CreateBatchActionBlock(BlockItemAction<T> done) => new BatchActionBlock<T>(
        boundedCapacity ?? batchSize,
        batchSize,
        batchTriggerPeriod != TimeSpan.Zero ? batchTriggerPeriod : Timeout.InfiniteTimeSpan,
        degreeOfParallelism,
        action,
        done,
        cancellationToken);
}