using System.Threading.Tasks.Dataflow;

namespace Youscan.Core.Pipes;

public class RoutingBlock<T> : ITargetBlock<T>
{
    readonly Func<T, ITargetBlock<T>> route;

    public RoutingBlock(Func<T, ITargetBlock<T>> route) => 
        this.route = route;

    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T>? source, bool consumeToAccept) => 
        route(messageValue).OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    public void Complete() {}
    public void Fault(Exception exception){}

    public Task Completion => Task.CompletedTask;
}

public static class RoutingBlockExtensions
{
    public static void LinkTo<T>(this ISourceBlock<T> source, Func<T, ITargetBlock<T>> route)
    {
        source.LinkTo(new RoutingBlock<T>(route));
    }
}