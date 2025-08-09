using System.Collections;

namespace Simpipe.Pipes;

/// <summary>
/// Represents a managed collection of connected pipes that provides pipeline-level operations and coordination.
/// </summary>
/// <typeparam name="T">The type of items flowing through the pipeline.</typeparam>
/// <remarks>
/// <para>
/// Pipeline provides centralized management for a collection of pipes:
/// • Automatic linking of pipes in sequence  
/// • Targeted item sending to specific pipes by ID
/// • Coordinated completion of all pipes
/// • Optional default routing logic across all pipes
/// • Enumeration of contained pipes for monitoring
/// </para>
/// <para>
/// Pipelines maintain pipes in the order they were added and automatically link each pipe to the next.
/// This creates a default processing chain while still allowing custom routing within individual pipes.
/// </para>
/// </remarks>
/// <example>
/// Creating and managing a pipeline:
/// <code>
/// var pipeline = new Pipeline&lt;Tweet&gt;();
/// 
/// // Add pipes - they're automatically linked in sequence
/// pipeline.Add(Pipe&lt;Tweet&gt;.Action(ModerateTweet).Id("moderator").ToPipe());
/// pipeline.Add(Pipe&lt;Tweet&gt;.Action(EnrichTweet).Id("enricher").ToPipe()); 
/// pipeline.Add(Pipe&lt;Tweet&gt;.Batch(500, IndexTweets).Id("indexer").ToPipe());
/// 
/// // Send to pipeline head
/// await pipeline.Send(tweet);
/// 
/// // Or send to specific pipe
/// await pipeline.Send(tweet, "enricher");
/// 
/// // Complete entire pipeline
/// await pipeline.Complete();
/// </code>
/// </example>
/// <summary>
/// Initializes a new instance of the <see cref="Pipeline{T}"/> class with optional default routing logic.
/// </summary>
/// <param name="defaultRoute">An optional function that provides default routing logic for all pipes in the pipeline. 
/// This function is called when a pipe doesn't have specific routing configured.</param>
/// <remarks>
/// <para>
/// The default route function is added to every pipe in the pipeline as a fallback routing option.
/// It's evaluated after any pipe-specific routing predicates and before using the next pipe in the chain.
/// </para>
/// <para>
/// Default routing is useful for cross-cutting concerns like error handling, auditing, or 
/// conditional processing that applies to multiple pipes.
/// </para>
/// </remarks>
/// <example>
/// Pipeline with default routing for blocked tweets:
/// <code>
/// var quarantinePipe = CreateQuarantinePipe();
/// 
/// var pipeline = new Pipeline&lt;Tweet&gt;(tweet =&gt; 
///     tweet.Status == TweetStatus.Blocked ? quarantinePipe : null);
/// 
/// // All pipes in this pipeline will route blocked tweets to quarantine
/// pipeline.Add(moderatorPipe);
/// pipeline.Add(enricherPipe);
/// pipeline.Add(indexerPipe);
/// </code>
/// </example>
public class Pipeline<T>(Func<T, Pipe<T>?>? defaultRoute = null) : IEnumerable<Pipe<T>>
{
    Pipe<T>? head;
    Pipe<T>? last;

    readonly TaskCompletionSource completion = new();

    readonly Dictionary<string, Pipe<T>> pipesById = new();
    readonly List<Pipe<T>> pipes = [];

    /// <summary>
    /// Adds a pipe to the pipeline and automatically links it to the previous pipe in the sequence.
    /// </summary>
    /// <param name="pipe">The pipe to add to the pipeline. Cannot be null and must have a unique ID.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipe"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a pipe with the same ID already exists in the pipeline.</exception>
    /// <remarks>
    /// <para>
    /// Pipes are automatically linked in the order they're added:
    /// • The first pipe becomes the pipeline head
    /// • Each subsequent pipe is linked as the "next" pipe of the previous one
    /// • Default routing (if configured) is applied to all pipes
    /// </para>
    /// <para>
    /// Pipe IDs must be unique within the pipeline to enable targeted operations.
    /// </para>
    /// </remarks>
    public void Add(Pipe<T> pipe)
    {
        if (!pipesById.TryAdd(pipe.Id, pipe))
            throw new Exception($"The pipe with id {pipe.Id} already exists");

        pipes.Add(pipe);

        head ??= pipe;
        Link(pipe);

        last = pipe;
    }

    void Link(Pipe<T> pipe)
    {
        if (defaultRoute != null)
            pipe.LinkTo(defaultRoute);

        last?.LinkNext(pipe);
    }

    /// <summary>
    /// Asynchronously sends an item through the pipeline, either to the head pipe or to a specific pipe by ID.
    /// </summary>
    /// <param name="item">The item to send through the pipeline.</param>
    /// <param name="id">Optional ID of the specific pipe to target. If null, the item is sent to the head pipe.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="PipeNotFoundException">Thrown when the specified <paramref name="id"/> doesn't match any pipe in the pipeline.</exception>
    /// <remarks>
    /// <para>
    /// Sending to the pipeline head (id = null) is the most common operation and processes the item
    /// through the normal pipeline sequence with all linking and routing logic applied.
    /// </para>
    /// <para>
    /// Targeted sending (with id) bypasses the normal sequence and sends directly to the specified pipe.
    /// The item will still be subject to that pipe's filtering and routing logic.
    /// </para>
    /// </remarks>
    /// <example>
    /// Different ways to send items:
    /// <code>
    /// // Send to pipeline head - normal processing
    /// await pipeline.Send(order);
    /// 
    /// // Send directly to specific pipe - bypass earlier stages  
    /// await pipeline.Send(order, "processor");
    /// 
    /// // Batch sending
    /// foreach (var order in orders)
    ///     await pipeline.Send(order);
    /// </code>
    /// </example>
    public async Task Send(T item, string? id = null)
    {
        if (id == null)
        {
            await head!.Send(item);
            return;
        }

        if (!pipesById.TryGetValue(id, out var target))
            throw new PipeNotFoundException($"The pipe with id '{id}' does not exist");

        await target.Send(item);
    }

    /// <summary>
    /// Asynchronously sends an item directly to the next pipe after the specified source pipe, bypassing the source pipe's processing.
    /// </summary>
    /// <param name="item">The item to send to the next pipe.</param>
    /// <param name="id">The ID of the source pipe whose next pipe should receive the item.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified <paramref name="id"/> doesn't exist in the pipeline.</exception>
    /// <remarks>
    /// This method is primarily used for custom routing scenarios where you need to skip a specific pipe
    /// but continue with the normal pipeline sequence from the next pipe.
    /// </remarks>
    public async Task SendNext(T item, string id)
    {
        if (!pipesById.TryGetValue(id, out var source))
            throw new Exception($"The pipe with id '{id}' does not exist");

        await source.SendNext(item);
    }

    /// <summary>
    /// Asynchronously completes all pipes in the pipeline and waits for all processing to finish.
    /// </summary>
    /// <returns>A task that represents the completion of all pipeline operations.</returns>
    /// <remarks>
    /// <para>
    /// Completion process:
    /// 1. Complete() is called on each pipe in the pipeline
    /// 2. Each pipe's Completion task is awaited to ensure all processing finishes
    /// 3. The pipeline's overall Completion task is marked as complete
    /// </para>
    /// <para>
    /// This method provides coordinated shutdown of the entire pipeline. Always call this method
    /// and await its completion to ensure proper resource cleanup and graceful shutdown.
    /// </para>
    /// </remarks>
    /// <example>
    /// Proper pipeline lifecycle management:
    /// <code>
    /// try
    /// {
    ///     // Process items
    ///     foreach (var item in items)
    ///         await pipeline.Send(item);
    /// }
    /// finally
    /// {
    ///     // Always complete the pipeline
    ///     await pipeline.Complete();
    /// }
    /// </code>
    /// </example>
    public async Task Complete()
    {
        foreach (var pipe in pipes)
        {
            pipe.Complete();
            await pipe.Completion;
        }

        completion.SetResult();
    }

    /// <summary>
    /// Gets a task that completes when the entire pipeline has been completed and all processing has finished.
    /// </summary>
    /// <value>A task representing the completion state of the entire pipeline.</value>
    /// <remarks>
    /// This task completes only after Complete() has been called and all individual pipes have finished processing.
    /// Use this for monitoring pipeline completion status or implementing dependent operations.
    /// </remarks>
    public Task Completion => completion.Task;

    public IEnumerator<Pipe<T>> GetEnumerator() => pipes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The exception that is thrown when attempting to access a pipe by ID that doesn't exist in the pipeline.
/// </summary>
/// <remarks>
/// This exception is thrown by pipeline operations that target specific pipes by ID, such as:
/// • <see cref="Pipeline{T}.Send(T, string)"/> with invalid pipe ID
/// • <see cref="Pipeline{T}.SendNext(T, string)"/> with invalid pipe ID
/// </remarks>
/// <example>
/// Handling pipe not found scenarios:
/// <code>
/// try
/// {
///     await pipeline.Send(item, "non-existent-pipe");
/// }
/// catch (PipeNotFoundException ex)
/// {
///     Console.WriteLine($"Pipe not found: {ex.Message}");
///     // Fallback to head pipe
///     await pipeline.Send(item);
/// }
/// </code>
/// </example>
public class PipeNotFoundException(string message) : Exception(message);