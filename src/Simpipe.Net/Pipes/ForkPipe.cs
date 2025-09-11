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

    /// <summary>
    /// Sets the unique identifier for the fork pipe being built.
    /// </summary>
    /// <param name="value">The unique identifier string. Cannot be null or empty.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The pipe ID is used for pipeline management operations like targeted sending and monitoring.
    /// IDs must be unique within a pipeline to avoid conflicts.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public ForkPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    /// <summary>
    /// Sets the filtering predicate that determines which items should be processed by this fork pipe.
    /// </summary>
    /// <param name="value">A predicate function that returns true for items that should be processed. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Items that don't match the filter are automatically forwarded to the next pipe in the chain,
    /// maintaining pipeline flow while allowing selective fork processing.
    /// </para>
    /// <para>
    /// Filtering is applied before items are sent to the parallel blocks, providing early rejection
    /// and avoiding unnecessary fork-join overhead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public ForkPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    /// <summary>
    /// Sets the routing function that determines where items should be sent after all parallel blocks complete processing.
    /// </summary>
    /// <param name="value">A function that takes a processed item and returns the target pipe, or null for default routing. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Routing is applied after all parallel blocks have completed processing and any join action has executed.
    /// This provides final control over where items go after the fork-join operation completes.
    /// </para>
    /// <para>
    /// Returning null from the routing function causes items to be sent to the next pipe in the chain.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
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

    /// <summary>
    /// Builds and returns the configured fork pipe.
    /// </summary>
    /// <returns>A new <see cref="Pipe{T}"/> instance configured for fork-join processing according to the builder settings.</returns>
    /// <remarks>
    /// This method creates a new fork pipe instance with all the configured parallel blocks and settings.
    /// The builder can be reused to create multiple pipes with the same configuration.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when no parallel blocks have been provided to the fork pipe.</exception>
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

    /// <summary>
    /// Implicitly converts a <see cref="ForkPipeBuilder{T}"/> to a <see cref="Pipe{T}"/> by building the configured fork pipe.
    /// </summary>
    /// <param name="builder">The builder to convert to a pipe.</param>
    /// <returns>A new <see cref="Pipe{T}"/> instance with the builder's fork-join configuration.</returns>
    /// <remarks>
    /// This conversion allows ForkPipeBuilder to be used directly in contexts expecting a Pipe,
    /// providing a seamless fluent interface experience.
    /// </remarks>
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

/// <summary>
/// Defines the contract for builders that create parallel blocks used in fork-join operations.
/// </summary>
/// <typeparam name="T">The type of items that will be processed by the parallel block.</typeparam>
/// <remarks>
/// <para>
/// Parallel block builders are used to configure and create individual processing blocks
/// that participate in fork-join parallelism within fork pipes.
/// </para>
/// <para>
/// Each parallel block builder produces a block that:
/// • Has a unique identifier within the fork
/// • Processes items independently from other blocks
/// • Can have its own configuration (parallelism, filtering, etc.)
/// • Participates in the fork-join synchronization
/// </para>
/// </remarks>
public interface IParallelBlockBuilder<T>
{
    /// <summary>
    /// Gets the unique identifier for this parallel block within the fork operation.
    /// </summary>
    /// <value>The unique identifier string for the block.</value>
    /// <remarks>
    /// Block IDs must be unique within a fork pipe to enable proper coordination and monitoring.
    /// </remarks>
    string Id { get; }
    
    /// <summary>
    /// Creates the configured parallel block with the specified completion action.
    /// </summary>
    /// <param name="done">The action to execute when the block completes processing an item.</param>
    /// <returns>A configured <see cref="IActionBlock{T}"/> ready for parallel processing.</returns>
    /// <remarks>
    /// The done action is called by the parallel block after successfully processing each item.
    /// This enables the fork-join coordination to track completion across all parallel blocks.
    /// </remarks>
    IActionBlock<T> ToBlock(BlockItemAction<T> done);
}

/// <summary>
/// Provides a fluent interface for configuring and building parallel action blocks used in fork-join operations.
/// </summary>
/// <typeparam name="T">The type of items that will be processed by the parallel block.</typeparam>
/// <remarks>
/// <para>
/// ParallelActionBlockBuilder creates individual processing blocks that execute actions on items
/// within a fork-join parallel processing scenario. Each block can have independent configuration
/// for parallelism, filtering, and capacity settings.
/// </para>
/// <para>
/// These blocks are components of fork pipes and participate in coordinated parallel processing
/// where all blocks must complete before the item continues to the next stage.
/// </para>
/// </remarks>
public class ParallelActionBlockBuilder<T>(BlockItemAction<T> action) : IParallelBlockBuilder<T>
{
    string id = "block-id";
    Func<T, bool>? filter;

    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    /// <summary>
    /// Sets the unique identifier for this parallel block within the fork operation.
    /// </summary>
    /// <param name="value">The unique identifier string. Cannot be null or empty.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Block IDs must be unique within a fork pipe to enable proper coordination and monitoring.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public ParallelActionBlockBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    /// <summary>
    /// Sets the filtering predicate that determines which items should be processed by this parallel block.
    /// </summary>
    /// <param name="value">A predicate function that returns true for items that should be processed. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Items that don't match the filter are automatically forwarded without processing,
    /// but the block still participates in the fork-join synchronization.
    /// </para>
    /// <para>
    /// Filtering at the block level allows selective processing within fork operations,
    /// enabling different blocks to operate on different subsets of items.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public ParallelActionBlockBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token for graceful shutdown of this parallel block's processing operations.
    /// </summary>
    /// <param name="value">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When cancellation is requested for this block:
    /// • New items are no longer accepted for processing
    /// • Currently executing operations are allowed to complete or respond to cancellation
    /// • The block participates in coordinated fork-join cancellation
    /// </para>
    /// <para>
    /// Individual blocks can have different cancellation tokens, allowing fine-grained control
    /// over which parts of a fork operation should be cancelled.
    /// </para>
    /// </remarks>
    public ParallelActionBlockBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Sets the degree of parallelism for concurrent item processing within this parallel block.
    /// </summary>
    /// <param name="value">The maximum number of items that can be processed concurrently within this block. Must be greater than 0.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each parallel block in a fork operation can have independent parallelism settings,
    /// allowing optimization for different types of operations within the same fork.
    /// </para>
    /// <para>
    /// Guidelines for block parallelism:
    /// • CPU-bound blocks: Use Environment.ProcessorCount or slightly higher
    /// • I/O-bound blocks: Use higher values based on I/O latency characteristics
    /// • Resource-constrained blocks: Use lower values to control resource usage
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public ParallelActionBlockBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    /// <summary>
    /// Sets the bounded capacity for this parallel block's internal processing queue to control memory usage.
    /// </summary>
    /// <param name="value">The maximum number of items that can be queued for processing in this block, or null for default capacity (parallelism * 2).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each parallel block maintains its own internal queue with independent capacity limits.
    /// This allows fine-grained memory control within fork operations.
    /// </para>
    /// <para>
    /// When a block's queue is full, the fork operation may wait for space to become available,
    /// providing natural back-pressure coordination across all parallel blocks.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
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

/// <summary>
/// Provides a fluent interface for configuring and building parallel batch action blocks used in fork-join operations.
/// </summary>
/// <typeparam name="T">The type of items that will be batched and processed by the parallel block.</typeparam>
/// <remarks>
/// <para>
/// ParallelBatchActionBlockBuilder creates batch processing blocks that group items into fixed-size batches
/// within a fork-join parallel processing scenario. Each block can have independent configuration
/// for batch size, trigger periods, parallelism, and filtering.
/// </para>
/// <para>
/// These batch blocks are components of fork pipes and participate in coordinated parallel processing
/// where all blocks must complete before items continue to the next stage.
/// </para>
/// </remarks>
public sealed class ParallelBatchActionBlockBuilder<T>(int batchSize, BlockItemAction<T> action) : IParallelBlockBuilder<T>
{
    string id = "block-id";
    Func<T, bool>? filter;

    TimeSpan batchTriggerPeriod;
    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    /// <summary>
    /// Sets the unique identifier for this parallel batch block within the fork operation.
    /// </summary>
    /// <param name="value">The unique identifier string. Cannot be null or empty.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Block IDs must be unique within a fork pipe to enable proper coordination and monitoring.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public ParallelBatchActionBlockBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    /// <summary>
    /// Sets the filtering predicate that determines which items should be included in batches by this parallel block.
    /// </summary>
    /// <param name="value">A predicate function that returns true for items that should be processed. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Items that don't match the filter are automatically forwarded without being added to batches,
    /// but the block still participates in the fork-join synchronization.
    /// </para>
    /// <para>
    /// Filtering at the block level allows selective batch processing within fork operations,
    /// enabling different blocks to operate on different subsets of items.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public ParallelBatchActionBlockBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    /// <summary>
    /// Sets the time-based trigger that flushes incomplete batches after the specified period.
    /// </summary>
    /// <param name="value">The maximum time to wait before flushing an incomplete batch. Use TimeSpan.Zero to disable time-based triggering.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Time-based triggering ensures that incomplete batches within this parallel block don't remain
    /// unprocessed indefinitely, which is crucial for maintaining fork-join coordination.
    /// </para>
    /// <para>
    /// Each parallel batch block in a fork can have independent batch trigger periods,
    /// allowing different blocks to optimize for their specific timing requirements.
    /// </para>
    /// </remarks>
    public ParallelBatchActionBlockBuilder<T> BatchTriggerPeriod(TimeSpan value)
    {
        batchTriggerPeriod = value;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token for graceful shutdown of this parallel batch block's processing operations.
    /// </summary>
    /// <param name="value">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When cancellation is requested for this batch block:
    /// • New items are no longer accepted for batching
    /// • Current incomplete batches are processed if possible
    /// • Currently executing batch operations are allowed to complete or respond to cancellation
    /// • The block participates in coordinated fork-join cancellation
    /// </para>
    /// <para>
    /// Individual batch blocks can have different cancellation tokens, allowing fine-grained control
    /// over which parts of a fork operation should be cancelled.
    /// </para>
    /// </remarks>
    public ParallelBatchActionBlockBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Sets the degree of parallelism for concurrent batch processing within this parallel block.
    /// </summary>
    /// <param name="value">The maximum number of batches that can be processed concurrently within this block. Must be greater than 0.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each parallel batch block in a fork operation can have independent parallelism settings,
    /// allowing optimization for different types of batch operations within the same fork.
    /// </para>
    /// <para>
    /// Batch parallelism operates at the batch level, not individual items. Multiple batches
    /// can be processed concurrently within this block.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public ParallelBatchActionBlockBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    /// <summary>
    /// Sets the bounded capacity for this parallel batch block's internal item queue to control memory usage.
    /// </summary>
    /// <param name="value">The maximum number of individual items (not batches) that can be queued in this block, or null for default capacity (batch size).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each parallel batch block maintains its own internal item queue with independent capacity limits.
    /// This allows fine-grained memory control within fork operations.
    /// </para>
    /// <para>
    /// When a block's queue is full, the fork operation may wait for space to become available,
    /// providing natural back-pressure coordination across all parallel blocks.
    /// </para>
    /// <para>
    /// The capacity represents individual items, not batches. For example, capacity 1000 with batch size 100
    /// means approximately 10 full batches can be queued in this block.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
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