namespace Simpipe.Blocks;

/// <summary>
/// Defines the contract for pipeline processing blocks that provide performance metrics and processing capabilities.
/// </summary>
/// <remarks>
/// <para>
/// IBlock is the foundational interface for all processing components in Simpipe.Net. It provides
/// essential performance monitoring capabilities that are used throughout the pipeline system
/// for observability and bottleneck detection.
/// </para>
/// <para>
/// All concrete block implementations provide real-time metrics that can be used for:
/// • Performance monitoring and alerting
/// • Pipeline bottleneck identification  
/// • Dynamic load balancing decisions
/// • Capacity planning and tuning
/// </para>
/// </remarks>
/// <example>
/// Monitoring block performance:
/// <code>
/// void MonitorPipelineHealth(IEnumerable&lt;IBlock&gt; blocks)
/// {
///     foreach (var block in blocks)
///     {
///         var throughput = block.OutputCount / (DateTime.Now - startTime).TotalSeconds;
///         var backlog = block.InputCount - block.OutputCount;
///         
///         if (backlog > 1000)
///             Console.WriteLine($"Warning: Block has {backlog} items in backlog");
///             
///         if (block.WorkingCount > 50)
///             Console.WriteLine($"High concurrency: {block.WorkingCount} items processing");
///     }
/// }
/// </code>
/// </example>
public interface IBlock
{
    /// <summary>
    /// Gets the total number of items that have been sent to this block for processing since creation.
    /// </summary>
    /// <value>A non-negative integer representing the cumulative input count.</value>
    /// <remarks>
    /// <para>
    /// This counter increases each time an item is accepted by the block, regardless of whether
    /// processing has started or completed. It includes items that are queued, currently processing,
    /// and already completed.
    /// </para>
    /// <para>
    /// Use this metric to:
    /// • Measure total throughput over time
    /// • Calculate processing rates (items per second)
    /// • Detect pipeline bottlenecks (compare with OutputCount)
    /// </para>
    /// </remarks>
    int InputCount => 0;
    
    /// <summary>
    /// Gets the total number of items that have completed processing in this block since creation.
    /// </summary>
    /// <value>A non-negative integer representing the cumulative output count.</value>
    /// <remarks>
    /// <para>
    /// This counter increases when items finish processing successfully. Items that fail during
    /// processing may or may not be counted, depending on the specific block implementation.
    /// </para>
    /// <para>
    /// The difference between InputCount and OutputCount represents items that are either
    /// queued for processing or currently being processed (plus any failed items).
    /// </para>
    /// </remarks>
    int OutputCount => 0;
    
    /// <summary>
    /// Gets the current number of items actively being processed by this block.
    /// </summary>
    /// <value>A non-negative integer representing the current working count.</value>
    /// <remarks>
    /// <para>
    /// This represents items that have been dequeued for processing but haven't yet completed.
    /// The value fluctuates in real-time as items enter and exit the processing stage.
    /// </para>
    /// <para>
    /// Working count is bounded by the block's degree of parallelism configuration.
    /// A consistently high working count may indicate:
    /// • Processing bottlenecks or slow operations
    /// • Appropriate utilization of available parallelism
    /// • Need for parallelism tuning
    /// </para>
    /// </remarks>
    int WorkingCount => 0;
}