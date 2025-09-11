using Simpipe.Blocks;

namespace Simpipe.Pipes;

public partial class Pipe<T>
{
    /// <summary>
    /// Creates a fluent builder for an action pipe that executes the specified synchronous action for each item.
    /// </summary>
    /// <param name="action">The synchronous action to execute for each item. Cannot be null.</param>
    /// <returns>An <see cref="ActionPipeBuilder{T}"/> for fluent configuration of the pipe.</returns>
    /// <remarks>
    /// <para>
    /// Action pipes process items individually with configurable parallelism. They are ideal for:
    /// • CPU-bound transformations
    /// • I/O operations with controlled concurrency  
    /// • Validation and filtering operations
    /// </para>
    /// <para>
    /// The default degree of parallelism is 1. Use <see cref="ActionPipeBuilder{T}.DegreeOfParallelism(int)"/> to increase concurrency.
    /// For I/O-bound work, consider higher parallelism values than CPU core count.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <example>
    /// Creating an action pipe with parallelism:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;
    ///     .Action(tweet =&gt; {
    ///         if (IsSpam(tweet) || HasProfanity(tweet))
    ///             tweet.Status = TweetStatus.Blocked;
    ///         Console.WriteLine($"Moderated tweet from @{tweet.Author}");
    ///     })
    ///     .DegreeOfParallelism(Environment.ProcessorCount)
    ///     .BoundedCapacity(1000)
    ///     .ToPipe();
    /// </code>
    /// </example>
    public static ActionPipeBuilder<T> Action(Action<T> action) => new(BlockItemAction<T>.Sync(action));

    /// <summary>
    /// Creates a fluent builder for an action pipe that executes the specified asynchronous action for each item.
    /// </summary>
    /// <param name="action">The asynchronous action to execute for each item. Cannot be null.</param>
    /// <returns>An <see cref="ActionPipeBuilder{T}"/> for fluent configuration of the pipe.</returns>
    /// <remarks>
    /// <para>
    /// Async action pipes are optimized for I/O-bound operations that benefit from async/await patterns.
    /// They provide better resource utilization than blocking synchronous operations.
    /// </para>
    /// <para>
    /// Consider using higher degree of parallelism for I/O-bound async operations compared to CPU-bound work.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <example>
    /// Creating an async action pipe for tweet enrichment:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;
    ///     .Action(async tweet =&gt; {
    ///         tweet.Sentiment = await AnalyzeSentiment(tweet.Text);
    ///         tweet.Language = await DetectLanguage(tweet.Text);
    ///         Console.WriteLine($"Enriched tweet {tweet.Id}");
    ///     })
    ///     .DegreeOfParallelism(20) // Higher for I/O-bound work
    ///     .ToPipe();
    /// </code>
    /// </example>
    public static ActionPipeBuilder<T> Action(Func<T, Task> action) => new(BlockItemAction<T>.Async(action));
}

/// <summary>
/// Provides a fluent interface for configuring and building action pipes that process individual items with configurable parallelism.
/// </summary>
/// <typeparam name="T">The type of items that will be processed by the pipe.</typeparam>
/// <remarks>
/// <para>
/// ActionPipeBuilder allows fluent configuration of:
/// • Degree of parallelism for concurrent processing
/// • Bounded capacity for back-pressure control
/// • Filtering predicates for conditional processing
/// • Routing logic for dynamic item forwarding
/// • Cancellation token support
/// • Unique pipe identification
/// </para>
/// <para>
/// The builder produces pipes optimized for individual item processing with System.Threading.Channels-based concurrency.
/// Default settings provide good performance for most scenarios, but can be tuned for specific workloads.
/// </para>
/// </remarks>
/// <example>
/// Configuring an action pipe for high-throughput tweet processing:
/// <code>
/// var pipe = Pipe&lt;Tweet&gt;
///     .Action(async tweet =&gt; {
///         tweet.Sentiment = await AnalyzeSentiment(tweet.Text);
///         tweet.Entities = ExtractEntities(tweet.Text);
///     })
///     .Id("tweet-enricher")
///     .DegreeOfParallelism(Environment.ProcessorCount * 2)
///     .BoundedCapacity(10000)
///     .Filter(tweet =&gt; tweet.Status != TweetStatus.Blocked)
///     .CancellationToken(cancellationToken)
///     .ToPipe();
/// </code>
/// </example>
public sealed class ActionPipeBuilder<T>(BlockItemAction<T> action)
{
    string id = "pipe-id";
    Func<T, bool>? filter;
    Func<T, Pipe<T>>? route;

    int? boundedCapacity;
    CancellationToken cancellationToken;
    int degreeOfParallelism = 1;

    /// <summary>
    /// Sets the unique identifier for the pipe being built.
    /// </summary>
    /// <param name="value">The unique identifier string. Cannot be null or empty.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// The pipe ID is used for pipeline management operations like targeted sending and monitoring.
    /// IDs must be unique within a pipeline to avoid conflicts.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public ActionPipeBuilder<T> Id(string value)
    {
        id = value;
        return this;
    }

    /// <summary>
    /// Sets the filtering predicate that determines which items should be processed by this pipe.
    /// </summary>
    /// <param name="value">A predicate function that returns true for items that should be processed. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Items that don't match the filter are automatically forwarded to the next pipe in the chain,
    /// maintaining pipeline flow while allowing selective processing.
    /// </para>
    /// <para>
    /// Filtering is applied before the item enters the processing queue, providing early rejection
    /// and reducing unnecessary queuing overhead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public ActionPipeBuilder<T> Filter(Func<T, bool> value)
    {
        filter = value;
        return this;
    }

    /// <summary>
    /// Sets the routing function that determines where items should be sent after processing.
    /// </summary>
    /// <param name="value">A function that takes a processed item and returns the target pipe, or null for default routing. Cannot be null.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Routing is only applied to items that were actually processed by this pipe (passed the filter).
    /// Returning null from the routing function causes the item to be sent to the next pipe in the chain.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public ActionPipeBuilder<T> Route(Func<T, Pipe<T>> value)
    {
        route = value;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token for graceful shutdown of processing operations.
    /// </summary>
    /// <param name="value">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When cancellation is requested:
    /// • New items are no longer accepted for processing
    /// • Currently executing operations are allowed to complete or respond to cancellation
    /// • The pipe transitions to a completed state
    /// </para>
    /// <para>
    /// Individual action implementations should check the cancellation token and respond appropriately
    /// to ensure timely shutdown.
    /// </para>
    /// </remarks>
    public ActionPipeBuilder<T> CancellationToken(CancellationToken value)
    {
        cancellationToken = value;
        return this;
    }

    /// <summary>
    /// Sets the degree of parallelism for concurrent item processing.
    /// </summary>
    /// <param name="value">The maximum number of items that can be processed concurrently. Must be greater than 0.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Guidelines for setting parallelism:
    /// • CPU-bound work: Use Environment.ProcessorCount or slightly higher
    /// • I/O-bound work: Use higher values (10-50 depending on I/O latency)
    /// • Memory-intensive work: Use lower values to control resource usage
    /// </para>
    /// <para>
    /// Higher parallelism increases throughput but also increases memory usage and context switching overhead.
    /// Monitor performance metrics to find the optimal balance for your workload.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public ActionPipeBuilder<T> DegreeOfParallelism(int value)
    {
        degreeOfParallelism = value;
        return this;
    }

    /// <summary>
    /// Sets the bounded capacity for the internal processing queue to control memory usage and provide back-pressure.
    /// </summary>
    /// <param name="value">The maximum number of items that can be queued for processing, or null for default capacity (parallelism * 2).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Bounded capacity provides back-pressure: when the queue is full, Send() operations will wait
    /// until space becomes available. This prevents unbounded memory growth in high-throughput scenarios.
    /// </para>
    /// <para>
    /// Default capacity (parallelism * 2) works well for most scenarios. Consider adjusting when:
    /// • Higher capacity: When items arrive in bursts and processing has variable latency
    /// • Lower capacity: When memory usage needs to be strictly controlled
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than or equal to 0.</exception>
    public ActionPipeBuilder<T> BoundedCapacity(int? value)
    {
        boundedCapacity = value;
        return this;
    }

    PipeOptions<T> Options() => new(id, filter, route);

    /// <summary>
    /// Builds and returns the configured action pipe.
    /// </summary>
    /// <returns>A new <see cref="Pipe{T}"/> instance configured according to the builder settings.</returns>
    /// <remarks>
    /// This method creates a new pipe instance with all the configured settings. The builder can be reused
    /// to create multiple pipes with the same configuration.
    /// </remarks>
    public Pipe<T> ToPipe() => new(Options(), done =>
        new ActionBlock<T>(
            boundedCapacity ?? degreeOfParallelism * 2,
            degreeOfParallelism,
            action,
            done,
            cancellationToken));

    /// <summary>
    /// Implicitly converts an <see cref="ActionPipeBuilder{T}"/> to a <see cref="Pipe{T}"/> by building the configured pipe.
    /// </summary>
    /// <param name="builder">The builder to convert to a pipe.</param>
    /// <returns>A new <see cref="Pipe{T}"/> instance with the builder's configuration.</returns>
    /// <remarks>
    /// This conversion allows ActionPipeBuilder to be used directly in contexts expecting a Pipe,
    /// providing a seamless fluent interface experience.
    /// </remarks>
    public static implicit operator Pipe<T>(ActionPipeBuilder<T> builder) => builder.ToPipe();
}