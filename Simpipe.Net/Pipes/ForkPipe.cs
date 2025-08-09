using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    /// <summary>
    /// Creates a fluent builder for a fork pipe that executes multiple parallel blocks on each item using fork-join parallelism.
    /// </summary>
    /// <param name="blocks">The parallel block builders that will process each item concurrently. Must contain at least one block.</param>
    /// <returns>A <see cref="ForkPipeBuilder{T}"/> for fluent configuration of the pipe.</returns>
    /// <remarks>
    /// <para>
    /// Fork pipes implement the fork-join pattern where:
    /// • Each item is sent to ALL specified parallel blocks simultaneously
    /// • All blocks must complete processing before the item continues to the next pipe
    /// • Blocks can execute different operations with independent parallelism settings
    /// • The join operation is executed after all blocks complete (if configured)
    /// </para>
    /// <para>
    /// Fork pipes are ideal for scenarios requiring multiple concurrent operations on the same data:
    /// • Validation and enrichment in parallel
    /// • Multiple transformations or calculations  
    /// • Parallel logging/auditing operations
    /// • Independent processing workflows that must complete together
    /// </para>
    /// <para>
    /// Use <see cref="ForkPipeBuilder{T}.Join(Action{T})"/> to execute code after all parallel blocks complete.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="blocks"/> is empty.</exception>
    /// <example>
    /// Creating a fork pipe for parallel tweet enrichment:
    /// <code>
    /// var sentimentBlock = Parallel&lt;Tweet&gt;
    ///     .Action(async tweet =&gt; tweet.Sentiment = await AnalyzeSentiment(tweet.Text))
    ///     .Id("sentiment")
    ///     .DegreeOfParallelism(3);
    ///
    /// var languageBlock = Parallel&lt;Tweet&gt;
    ///     .Action(async tweet =&gt; tweet.Language = await DetectLanguage(tweet.Text))
    ///     .Id("language")  
    ///     .DegreeOfParallelism(2);
    ///
    /// var entitiesBlock = Parallel&lt;Tweet&gt;
    ///     .Action(tweet =&gt; tweet.Entities = ExtractEntities(tweet.Text))
    ///     .Id("entities");
    ///
    /// var forkPipe = Pipe&lt;Tweet&gt;
    ///     .Fork(sentimentBlock, languageBlock, entitiesBlock)
    ///     .Join(tweet =&gt; Console.WriteLine($"Enriched tweet {tweet.Id}"))
    ///     .Id("enrichment-fork")
    ///     .ToPipe();
    /// </code>
    /// </example>
    public static ForkPipeBuilder<T> Fork(params IParallelBlockBuilder<T>[] blocks) => new(blocks);
}

/// <summary>
/// Provides a fluent interface for configuring and building fork pipes that implement fork-join parallelism 
/// by executing multiple parallel blocks on each item simultaneously.
/// </summary>
/// <typeparam name="T">The type of items that will be processed by the parallel blocks.</typeparam>
/// <remarks>
/// <para>
/// Fork pipes implement the fork-join pattern where each item is processed by multiple parallel blocks
/// concurrently, and execution continues only after all blocks have completed processing the item.
/// </para>
/// <para>
/// Key characteristics:
/// • All parallel blocks receive the same input item
/// • Each block can have independent configuration (parallelism, filtering, etc.)
/// • The item proceeds to the next pipe only after ALL blocks complete
/// • An optional join action can be executed after all blocks finish
/// </para>
/// <para>
/// Fork pipes are ideal for scenarios requiring multiple concurrent operations on the same data.
/// </para>
/// </remarks>
/// <example>
/// Creating a fork pipe with validation and logging blocks:
/// <code>
/// var validateBlock = Parallel&lt;Order&gt;
///     .Action(async order =&gt; {
///         if (!await ValidateOrderAsync(order))
///             throw new ValidationException($"Invalid order: {order.Id}");
///     })
///     .Id("validator")
///     .DegreeOfParallelism(3);
/// 
/// var auditBlock = Parallel&lt;Order&gt;
///     .Action(order =&gt; LogOrderProcessing(order.Id, "Started"))
///     .Id("auditor");
/// 
/// var forkPipe = Pipe&lt;Order&gt;
///     .Fork(validateBlock, auditBlock)
///     .Join(order =&gt; LogOrderProcessing(order.Id, "Validated"))
///     .Id("order-fork")
///     .ToPipe();
/// </code>
/// </example>
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

    /// <summary>
    /// Sets the join action that will be executed after all parallel blocks have completed processing an item.
    /// </summary>
    /// <param name="value">The action to execute after all blocks complete. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The join action is executed exactly once per item after all parallel blocks have successfully completed.
    /// It provides a synchronization point to perform operations that depend on all parallel work being finished.
    /// </para>
    /// <para>
    /// Common uses for join actions:
    /// • Logging completion of parallel operations
    /// • Final validation or calculation based on all block results
    /// • Triggering dependent workflows
    /// • Updating completion status or metrics
    /// </para>
    /// <para>
    /// If any parallel block fails with an exception, the join action is not executed for that item.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <example>
    /// Using join action for completion notification:
    /// <code>
    /// var forkPipe = Pipe&lt;Document&gt;
    ///     .Fork(validateBlock, enrichBlock, indexBlock)
    ///     .Join(document =&gt; {
    ///         document.ProcessingCompleted = DateTime.UtcNow;
    ///         NotifyCompletionAsync(document.Id);
    ///     })
    ///     .ToPipe();
    /// </code>
    /// </example>
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

/// <summary>
/// Provides static factory methods for creating parallel block builders used in fork-join scenarios.
/// </summary>
/// <typeparam name="T">The type of items that will be processed by the parallel blocks.</typeparam>
/// <remarks>
/// <para>
/// Parallel blocks are used within fork pipes to implement fork-join parallelism. Each parallel block
/// represents one branch of the fork that processes items independently with its own configuration.
/// </para>
/// <para>
/// Key differences from regular pipes:
/// • Parallel blocks are components of fork pipes, not standalone pipes
/// • They don't have next pipe linking (managed by the fork pipe)
/// • They can have independent parallelism, filtering, and capacity settings
/// • They participate in the fork-join synchronization pattern
/// </para>
/// </remarks>
/// <example>
/// Creating parallel blocks for a fork pipe:
/// <code>
/// var validateBlock = Parallel&lt;Order&gt;
///     .Action(async order =&gt; await ValidateOrderAsync(order))
///     .Id("validate")
///     .DegreeOfParallelism(3)
///     .Filter(order =&gt; order.RequiresValidation);
///
/// var enrichBlock = Parallel&lt;Order&gt;
///     .Batch(10, async orders =&gt; await EnrichOrdersAsync(orders))
///     .Id("enrich")
///     .BatchTriggerPeriod(TimeSpan.FromSeconds(2));
///
/// var forkPipe = Pipe&lt;Order&gt;
///     .Fork(validateBlock, enrichBlock)
///     .ToPipe();
/// </code>
/// </example>
public static class Parallel<T>
{
    /// <summary>
    /// Creates a builder for a parallel action block that executes the specified synchronous action for each item.
    /// </summary>
    /// <param name="action">The synchronous action to execute for each item. Cannot be null.</param>
    /// <returns>A <see cref="ParallelActionBlockBuilder{T}"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public static ParallelActionBlockBuilder<T> Action(Action<T> action) => new(BlockItemAction<T>.Sync(action));
    
    /// <summary>
    /// Creates a builder for a parallel action block that executes the specified asynchronous action for each item.
    /// </summary>
    /// <param name="action">The asynchronous action to execute for each item. Cannot be null.</param>
    /// <returns>A <see cref="ParallelActionBlockBuilder{T}"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public static ParallelActionBlockBuilder<T> Action(Func<T, Task> action) => new(BlockItemAction<T>.Async(action));

    /// <summary>
    /// Creates a builder for a parallel batch action block that groups items and executes the specified synchronous action on batches.
    /// </summary>
    /// <param name="batchSize">The size of batches to create. Must be greater than 0.</param>
    /// <param name="action">The synchronous action to execute for each batch. Cannot be null.</param>
    /// <returns>A <see cref="ParallelBatchActionBlockBuilder{T}"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public static ParallelBatchActionBlockBuilder<T> Batch(int batchSize, Action<T[]> action) => new(batchSize, BlockItemAction<T>.BatchSync(action));
    
    /// <summary>
    /// Creates a builder for a parallel batch action block that groups items and executes the specified asynchronous action on batches.
    /// </summary>
    /// <param name="batchSize">The size of batches to create. Must be greater than 0.</param>
    /// <param name="action">The asynchronous action to execute for each batch. Cannot be null.</param>
    /// <returns>A <see cref="ParallelBatchActionBlockBuilder{T}"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
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