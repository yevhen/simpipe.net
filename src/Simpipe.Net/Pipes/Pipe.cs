using Simpipe.Blocks;

namespace Simpipe.Pipes;

/// <summary>
/// Represents a composable pipeline stage that processes items of type <typeparamref name="T"/> using System.Threading.Channels for high-performance concurrent processing.
/// Pipes can be chained together, filtered, and routed to create complex data processing workflows.
/// </summary>
/// <typeparam name="T">The type of items flowing through the pipe.</typeparam>
/// <remarks>
/// <para>
/// Pipes are the fundamental building blocks of processing pipelines. Each pipe:
/// • Receives items and processes them through an underlying block
/// • Supports filtering to conditionally process items
/// • Can route items to different target pipes based on predicates
/// • Tracks performance metrics (input, output, working counts)
/// • Propagates completion through the pipeline chain
/// </para>
/// <para>
/// Use static factory methods like <see cref="Action(Action{T})"/> and <see cref="Batch(int, Action{T[]})"/> to create pipes with fluent configuration.
/// </para>
/// </remarks>
/// <example>
/// Basic pipe creation and chaining:
/// <code>
/// // Create a sentiment analysis pipe
/// var sentimentPipe = Pipe&lt;Tweet&gt;
///     .Action(tweet =&gt; tweet.Sentiment = AnalyzeSentiment(tweet.Text))
///     .Id("sentiment-analyzer")
///     .DegreeOfParallelism(4)
///     .ToPipe();
/// 
/// // Create a batch pipe for Elasticsearch indexing
/// var indexPipe = Pipe&lt;Tweet&gt;
///     .Batch(100, async tweets =&gt; {
///         await ElasticsearchClient.BulkIndex(tweets);
///         Console.WriteLine($"Indexed {tweets.Length} tweets");
///     })
///     .Id("elasticsearch-indexer")
///     .ToPipe();
/// 
/// // Chain them together
/// sentimentPipe.LinkNext(indexPipe);
/// 
/// // Process tweets
/// await sentimentPipe.Send(new Tweet { Text = "Love this product!" });
/// await sentimentPipe.Send(new Tweet { Text = "Great service @support" });
/// 
/// // Complete processing
/// sentimentPipe.Complete();
/// await sentimentPipe.Completion;
/// </code>
/// </example>
public partial class Pipe<T>
{
    Pipe<T>? next;

    readonly Func<T, bool>? filter;
    readonly List<Func<T, Pipe<T>?>> routes = [];
    readonly TaskCompletionSource completion = new();
    readonly IActionBlock<T> block;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipe{T}"/> class.
    /// </summary>
    /// <param name="options">The pipe configuration options.</param>
    /// <param name="blockFactory">Factory function to create the underlying processing block.</param>
    public Pipe(PipeOptions<T> options, Func<BlockItemAction<T>, IActionBlock<T>> blockFactory)
    {
        Id = options.Id;
        filter = options.Filter;

        var route = options.Route;
        if (route != null)
            routes.Add(route);

        var done = new BlockItemAction<T>(RouteItem);
        block = blockFactory(done);
    }

    /// <summary>
    /// Gets the unique identifier for this pipe, used for pipeline management and routing.
    /// </summary>
    /// <value>A string that uniquely identifies this pipe within a pipeline.</value>
    /// <remarks>
    /// The ID is set during pipe construction using the builder's <c>Id()</c> method.
    /// Pipeline operations like <see cref="Pipeline{T}.Send(T, string)"/> use this ID to target specific pipes.
    /// </remarks>
    public string Id { get; }
    
    /// <summary>
    /// Gets the underlying block that handles the actual processing logic for this pipe.
    /// </summary>
    /// <value>An <see cref="IBlock"/> instance that provides processing capabilities and performance metrics.</value>
    /// <remarks>
    /// <para>
    /// The block exposes performance metrics:
    /// • <see cref="IBlock.InputCount"/> - Total items received
    /// • <see cref="IBlock.OutputCount"/> - Total items successfully processed  
    /// • <see cref="IBlock.WorkingCount"/> - Items currently being processed
    /// </para>
    /// <para>
    /// Use these metrics for monitoring pipeline performance and identifying bottlenecks.
    /// </para>
    /// </remarks>
    /// <example>
    /// Monitoring pipe performance:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;.Action(ProcessTweet).Id("processor").ToPipe();
    /// 
    /// // Check processing metrics
    /// Console.WriteLine($"Input: {pipe.Block.InputCount}");
    /// Console.WriteLine($"Working: {pipe.Block.WorkingCount}");
    /// Console.WriteLine($"Output: {pipe.Block.OutputCount}");
    /// 
    /// // Detect potential bottlenecks
    /// if (pipe.Block.InputCount > pipe.Block.OutputCount * 2)
    ///     Console.WriteLine("Potential bottleneck detected");
    /// </code>
    /// </example>
    public IBlock Block => block;

    IActionBlock<T> Target(T item) => FilterMatches(item)
        ? block
        : RouteTarget(item);

    async Task RouteItem(BlockItem<T> item) => await item.Apply(RouteItem);
    async Task RouteItem(T item) => await RouteTarget(item).Send(item);

    IActionBlock<T> RouteTarget(T item)
    {
        var target = Route(item) ?? next;
        return target == null
            ? NullBlock<T>.Instance
            : target.Target(item);
    }

    Pipe<T>? Route(T item) => routes
        .Select(route => route(item))
        .FirstOrDefault(pipe => pipe != null);

    /// <summary>
    /// Asynchronously sends an item through this pipe for processing.
    /// </summary>
    /// <param name="item">The item to process through this pipe.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// <para>
    /// The send operation:
    /// 1. Applies the pipe's filter (if configured) - non-matching items are forwarded to the next pipe
    /// 2. If the item matches the filter, it's processed by this pipe's block
    /// 3. After processing completes, the item is routed to the next pipe or routing target
    /// </para>
    /// <para>
    /// This method provides back-pressure - if the pipe's internal channel is full, the send will wait
    /// until space becomes available. Use <see cref="ActionPipeBuilder{T}.BoundedCapacity(int?)"/> to control buffering.
    /// </para>
    /// <para>
    /// Items that don't match the pipe's filter are automatically forwarded to maintain pipeline flow.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the pipe has been completed and is no longer accepting items.</exception>
    /// <example>
    /// Sending items through a pipe:
    /// <code>
    /// var pipe = Pipe&lt;Tweet&gt;
    ///     .Action(tweet =&gt; Console.WriteLine($"Processing: @{tweet.Author}"))
    ///     .Filter(tweet =&gt; tweet.RetweetCount > 100)
    ///     .ToPipe();
    /// 
    /// await pipe.Send(viralTweet);    // Will be processed (high retweets)
    /// await pipe.Send(normalTweet);   // Will be forwarded (doesn't match filter)
    /// </code>
    /// </example>
    public async Task Send(T item)
    {
        if (FilterMatches(item))
        {
            await SendThis(item);
            return;
        }

        await SendNext(item);
    }

    bool FilterMatches(T item) => filter == null || filter(item);

    Task SendThis(T item) => BlockSend(item);

    /// <summary>
    /// Asynchronously sends an item directly to the next pipe in the chain, bypassing this pipe's processing.
    /// </summary>
    /// <param name="item">The item to send to the next pipe.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// <para>
    /// This method allows direct forwarding of items to the next pipe without processing them through
    /// the current pipe's block. It's primarily used internally for filter bypass and custom routing scenarios.
    /// </para>
    /// <para>
    /// If no next pipe is configured, the item is silently discarded (sent to a null block).
    /// </para>
    /// </remarks>
    /// <example>
    /// Manually forwarding items:
    /// <code>
    /// // In a custom pipe implementation
    /// if (shouldSkipProcessing)
    ///     await pipe.SendNext(item);
    /// else
    ///     await pipe.Send(item);
    /// </code>
    /// </example>
    public async Task SendNext(T item)
    {
        if (next != null)
            await next.Send(item);
    }

    /// <summary>
    /// Signals that no more items will be sent to this pipe and initiates graceful shutdown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Completion is a cooperative process:
    /// 1. The pipe stops accepting new items
    /// 2. All currently processing items are allowed to finish
    /// 3. Completion propagates to the next pipe in the chain
    /// 4. The <see cref="Completion"/> task completes when all processing is finished
    /// </para>
    /// <para>
    /// Always call Complete() and await Completion to ensure graceful pipeline shutdown and proper resource cleanup.
    /// </para>
    /// </remarks>
    /// <example>
    /// Proper pipeline completion:
    /// <code>
    /// // Process tweets
    /// await pipe.Send(new Tweet { Text = "Breaking news!" });
    /// await pipe.Send(new Tweet { Text = "Great product launch" });
    /// 
    /// // Signal completion and wait for all processing to finish
    /// pipe.Complete();
    /// await pipe.Completion;
    /// </code>
    /// </example>
    public void Complete() => BlockComplete();
    
    /// <summary>
    /// Gets a task that completes when this pipe has finished processing all items and has been gracefully shut down.
    /// </summary>
    /// <value>A task representing the completion of all processing activities.</value>
    /// <remarks>
    /// <para>
    /// This task will complete successfully when:
    /// • <see cref="Complete"/> has been called
    /// • All items currently being processed have finished
    /// • Any exceptions during processing have been handled
    /// </para>
    /// <para>
    /// The task may complete with an exception if processing encountered unhandled errors.
    /// Always await this task after calling <see cref="Complete"/> to ensure proper cleanup.
    /// </para>
    /// </remarks>
    public Task Completion => AwaitCompletion();

    async Task AwaitCompletion()
    {
        try
        {
            await BlockCompletion();
        }
        catch (TaskCanceledException) {}
    }

    /// <summary>
    /// Adds a routing predicate that determines where items should be sent after processing by this pipe.
    /// </summary>
    /// <param name="route">A function that takes an item and returns the target pipe, or null if the item should continue to the next pipe.</param>
    /// <remarks>
    /// <para>
    /// Routing predicates are evaluated in the order they were added. The first non-null result is used as the target.
    /// If all routing predicates return null, the item is sent to the next pipe (configured via <see cref="LinkNext"/>).
    /// </para>
    /// <para>
    /// Routing only applies to items that were actually processed by this pipe (i.e., items that passed the filter).
    /// Items that were filtered out are forwarded directly without routing evaluation.
    /// </para>
    /// <para>
    /// Multiple routes can be added to implement complex routing logic with fallback behavior.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    /// <example>
    /// Setting up conditional routing:
    /// <code>
    /// var englishPipe = CreateEnglishProcessingPipe();
    /// var spanishPipe = CreateSpanishProcessingPipe();
    /// var translationPipe = CreateTranslationPipe();
    /// 
    /// sourcePipe.LinkTo(tweet => {
    ///     return tweet.Language switch {
    ///         "en" => englishPipe,
    ///         "es" => spanishPipe,
    ///         _ => translationPipe
    ///     };
    /// });
    /// </code>
    /// </example>
    public void LinkTo(Func<T, Pipe<T>?> route) => routes.Add(route);
    
    /// <summary>
    /// Sets the default next pipe in the processing chain that will receive items after this pipe completes processing.
    /// </summary>
    /// <param name="next">The pipe that should receive items after processing, or null to terminate the chain at this pipe.</param>
    /// <remarks>
    /// <para>
    /// The next pipe serves as the default destination for items that:
    /// • Complete processing successfully in this pipe
    /// • Don't match any configured routing predicates
    /// • Are filtered out (don't match this pipe's filter)
    /// </para>
    /// <para>
    /// Setting next to null creates a terminal pipe - items will be discarded after processing.
    /// This is useful for sink operations like logging or metrics collection.
    /// </para>
    /// </remarks>
    /// <example>
    /// Creating a processing chain:
    /// <code>
    /// var moderatePipe = Pipe&lt;Tweet&gt;.Action(ModerateTweet).Id("moderator").ToPipe();
    /// var enrichPipe = Pipe&lt;Tweet&gt;.Action(EnrichTweet).Id("enricher").ToPipe();
    /// var indexPipe = Pipe&lt;Tweet&gt;.Action(IndexTweet).Id("indexer").ToPipe();
    /// 
    /// // Chain them together
    /// moderatePipe.LinkNext(enrichPipe);
    /// enrichPipe.LinkNext(indexPipe);
    /// // indexPipe has no next - terminates the chain
    /// </code>
    /// </example>
    public void LinkNext(Pipe<T>? next) => this.next = next;

    Task BlockSend(T item) => block.Send(item);

    void BlockComplete()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await block.Complete();
                completion.TrySetResult();
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
        });
    }

    Task BlockCompletion() => completion.Task;
}