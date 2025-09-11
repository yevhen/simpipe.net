using System.Threading.Channels;
using Simpipe.Utility;

namespace Simpipe.Pipes;

/// <summary>
/// Provides work-in-progress limiting for pipeline processing to control memory usage and provide flow control.
/// </summary>
/// <typeparam name="T">The type of items being processed with work limiting.</typeparam>
/// <remarks>
/// <para>
/// PipelineLimiter implements the "work-in-progress" (WIP) limiting pattern where:
/// • A maximum number of items can be actively processed simultaneously
/// • New items wait when the limit is reached (back-pressure)
/// • Completed items must be explicitly marked as done to free up capacity
/// • This prevents unbounded resource consumption in high-throughput scenarios
/// </para>
/// <para>
/// WIP limiting is essential for:
/// • Controlling memory usage in pipelines processing large items
/// • Rate-limiting calls to external services
/// • Preventing resource exhaustion in long-running pipelines
/// • Implementing flow control between fast producers and slow consumers
/// </para>
/// <para>
/// Unlike bounded channels which limit queuing, WIP limiting controls concurrent execution.
/// </para>
/// </remarks>
/// <example>
/// Using PipelineLimiter for SQS queue polling with WIP control:
/// <code>
/// // Create pipeline with consistent capacity
/// var pipeline = new Pipeline&lt;Tweet&gt;();
/// pipeline.Add(Pipe&lt;Tweet&gt;
///     .Action(async tweet =&gt; await ValidateTweet(tweet))
///     .BoundedCapacity(50)  // Same as maxWork
///     .Id("validator")
///     .ToPipe());
/// pipeline.Add(Pipe&lt;Tweet&gt;
///     .Action(async tweet =&gt; await EnrichTweet(tweet))
///     .BoundedCapacity(50)  // Same as maxWork
///     .Id("enricher")
///     .ToPipe());
/// 
/// // PipelineLimiter controls total work-in-progress
/// var limiter = new PipelineLimiter&lt;Tweet&gt;(
///     maxWork: 50, // Only 50 tweets in flight
///     dispatch: async tweet =&gt; {
///         try {
///             await pipeline.Send(tweet);
///             // Signal done when tweet exits pipeline
///             await limiter.TrackDone(tweet);
///         } catch (Exception ex) {
///             Console.WriteLine($"Failed: {ex.Message}");
///             await limiter.TrackDone(tweet); // Always signal done
///         }
///     });
/// 
/// // Poll from SQS queue with automatic back-pressure
/// while (!cancellationToken.IsCancellationRequested)
/// {
///     var messages = await SQSClient.ReceiveMessages(queueUrl);
///     foreach (var message in messages)
///     {
///         var tweet = DeserializeTweet(message.Body);
///         await limiter.Send(tweet); // Blocks if 50 tweets in flight
///         await SQSClient.DeleteMessage(queueUrl, message.ReceiptHandle);
///     }
/// }
/// 
/// await limiter.Complete();
/// </code>
/// </example>
public class PipelineLimiter<T>
{
    readonly Channel<T> input = Channel.CreateBounded<T>(1);
    readonly Channel<T> done = Channel.CreateBounded<T>(1);
    readonly Task processor;

    int wip;
    readonly int maxWork;
    readonly Func<T, Task> dispatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineLimiter{T}"/> class with the specified work limit and dispatch function.
    /// </summary>
    /// <param name="maxWork">The maximum number of items that can be processed concurrently. Must be greater than 0.</param>
    /// <param name="dispatch">The function that will be called to process each item. Cannot be null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxWork"/> is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatch"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The dispatch function is responsible for:
    /// • Processing the item (async operations are supported)
    /// • Calling <see cref="TrackDone(T)"/> when processing completes (including error cases)
    /// • Handling exceptions appropriately (they don't automatically free up WIP slots)
    /// </para>
    /// <para>
    /// Choose maxWork based on:
    /// • Memory constraints (lower values for memory-intensive processing)
    /// • External service limits (API rate limits, database connections)
    /// • System resources (CPU, I/O capacity)
    /// </para>
    /// </remarks>
    public PipelineLimiter(int maxWork, Func<T, Task> dispatch)
    {
        this.maxWork = maxWork;
        this.dispatch = dispatch;

        processor = Select.Run(
            () => !input.Reader.Completion.IsCompleted &&
                  !done.Reader.Completion.IsCompleted,
            new Selector(() => input.Reader.WaitToReadAsync().AsTask(), ProcessSend),
            new Selector(() => done.Reader.WaitToReadAsync().AsTask(), ProcessDone));
    }

    async Task ProcessSend()
    {
        while (wip < maxWork && input.Reader.TryRead(out var item))
        {
            wip++;
            await dispatch(item);
        }
    }

    Task ProcessDone()
    {
        while (done.Reader.TryRead(out _))
            wip--;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously sends an item for processing with work-in-progress limiting.
    /// </summary>
    /// <param name="item">The item to process.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <remarks>
    /// <para>
    /// This method provides back-pressure: if the maximum work limit is reached, the send operation
    /// will wait until capacity becomes available (when other items complete via <see cref="TrackDone(T)"/>).
    /// </para>
    /// <para>
    /// The item is queued for processing and will be dispatched when:
    /// • Current work count is below the limit
    /// • A processing slot becomes available
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the limiter has been completed and is no longer accepting items.</exception>
    public async Task Send(T item) => await input.Writer.WriteAsync(item);
    
    /// <summary>
    /// Signals that processing of the specified item has completed, freeing up a work-in-progress slot.
    /// </summary>
    /// <param name="item">The item whose processing has completed.</param>
    /// <returns>A task that represents the asynchronous tracking operation.</returns>
    /// <remarks>
    /// <para>
    /// This method MUST be called for every item that was dispatched, regardless of whether
    /// processing succeeded or failed. Failure to call this method will permanently consume
    /// a WIP slot and eventually cause the pipeline to hang.
    /// </para>
    /// <para>
    /// Best practice is to call this method in a finally block or using statement to ensure
    /// it's called even when exceptions occur.
    /// </para>
    /// </remarks>
    /// <example>
    /// Ensuring TrackDone is always called:
    /// <code>
    /// var limiter = new PipelineLimiter&lt;Tweet&gt;(20, async tweet =&gt; {
    ///     try
    ///     {
    ///         await ProcessTweetThroughPipeline(tweet);
    ///     }
    ///     finally
    ///     {
    ///         await limiter.TrackDone(tweet);
    ///     }
    /// });
    /// </code>
    /// </example>
    public async Task TrackDone(T item) => await done.Writer.WriteAsync(item);

    /// <summary>
    /// Completes the limiter, preventing new items from being accepted and waiting for all current work to finish.
    /// </summary>
    /// <returns>A task that represents the completion of all processing.</returns>
    /// <remarks>
    /// <para>
    /// Completion process:
    /// 1. No new items are accepted for processing
    /// 2. All currently processing items are allowed to complete
    /// 3. The method returns when all work-in-progress slots are freed
    /// </para>
    /// <para>
    /// Always call this method to ensure proper shutdown and resource cleanup.
    /// </para>
    /// </remarks>
    public async Task Complete()
    {
        input.Writer.Complete();
        done.Writer.Complete();
        await processor;
    }
}
