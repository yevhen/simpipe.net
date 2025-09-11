namespace Simpipe.Pipes;

/// <summary>
/// Encapsulates configuration options for creating and configuring pipes in the pipeline.
/// </summary>
/// <typeparam name="T">The type of items flowing through the configured pipe.</typeparam>
/// <param name="Id">The unique identifier for the pipe. Cannot be null or empty.</param>
/// <param name="Filter">Optional predicate to filter items before processing. Items not matching are forwarded without processing.</param>
/// <param name="Route">Optional function to determine dynamic routing after processing. Returns null to use default next pipe.</param>
/// <remarks>
/// <para>
/// PipeOptions provides a clean, immutable configuration object for pipe creation. It's used internally
/// by the pipe builder classes but can also be used directly when creating custom pipes.
/// </para>
/// <para>
/// The record's immutability ensures thread-safety and enables the with-expression pattern for creating
/// modified copies of configurations.
/// </para>
/// </remarks>
/// <example>
/// Creating pipes with different configurations:
/// <code>
/// // Basic pipe with just an ID
/// var basicOptions = new PipeOptions&lt;Order&gt;("order-processor");
/// 
/// // Pipe with filtering
/// var filterOptions = new PipeOptions&lt;Order&gt;(
///     Id: "priority-processor",
///     Filter: order => order.Priority == Priority.High
/// );
/// 
/// // Pipe with routing
/// var routeOptions = new PipeOptions&lt;Order&gt;(
///     Id: "order-router",
///     Route: order => order.IsInternational ? internationalPipe : domesticPipe
/// );
/// 
/// // Creating modified copy
/// var modifiedOptions = filterOptions with { 
///     Route = order => order.Value > 1000 ? largePipe : smallPipe 
/// };
/// 
/// // Using with Pipe constructor
/// var pipe = new Pipe&lt;Order&gt;(routeOptions, done => 
///     new ActionBlock&lt;Order&gt;(100, 4, ProcessOrder, done));
/// </code>
/// </example>
public record PipeOptions<T>(
    string Id,
    Func<T, bool>? Filter = null,
    Func<T, Pipe<T>?>? Route = null
);