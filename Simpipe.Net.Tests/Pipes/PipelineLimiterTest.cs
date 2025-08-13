using static SharpAssert.Sharp;

namespace Simpipe.Pipes;

[TestFixture]
public class PipelineLimiterTest
{
    [Test]
    public async Task Blocks_send_when_wip_is_at_max()
    {
        List<int> dispatched = [];

        var limiter = new PipelineLimiter<int>(maxWork: 1, x =>
        {
            dispatched.Add(x);
            return Task.CompletedTask;
        });

        await limiter.Send(1);
        await Task.Delay(10);
        Assert(dispatched.Count == 1);
        Assert(dispatched[0] == 1);

        await limiter.Send(2);
        await Task.Delay(10);
        Assert(dispatched.Count == 1);

        var sent = limiter.Send(3);
        await Task.Delay(10);
        Assert(sent.IsCompleted == false);
    }

    [Test]
    public async Task Decrements_wip_on_done()
    {
        List<int> dispatched = [];

        var limiter = new PipelineLimiter<int>(maxWork: 1, x =>
        {
            dispatched.Add(x);
            return Task.CompletedTask;
        });

        await limiter.Send(1);
        await limiter.Send(2);
        var sent = limiter.Send(3);

        await limiter.TrackDone(1);
        await sent;

        Assert(dispatched.Count == 2);
        Assert(dispatched[0] == 1);
        Assert(dispatched[1] == 2);

        await limiter.TrackDone(2);
        await limiter.Complete();
    }
}