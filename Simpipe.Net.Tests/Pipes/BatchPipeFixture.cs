using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

[TestFixture]
public class BatchPipeFixture
{
    Pipe<string> pipe = null!;

    [Test]
    public async Task Executes_action()
    {
        var executed = false;
        Setup(async _ =>
        {
            await Task.Delay(1);
            executed = true;
        });

        await Complete("foo");

        Assert.True(executed);
    }

    [Test]
    public void Awaits_completion()
    {
        Setup(async _ =>
        {
            await Task.Delay(1);
            throw new ArgumentException();
        });

        Assert.ThrowsAsync<ArgumentException>(() => Complete("boom"));
    }

    [Test]
    public async Task Completion_pushes_to_next()
    {
        Setup(async _ =>
        {
            await Task.Delay(100);
        });

        var nextReceived = new List<string>();
        var nextPipe = PipeMock<string>.Create(id: "next", nextReceived.Add);
        pipe.LinkNext(nextPipe);

        await Complete("foo");
        SpinWait.SpinUntil(() => nextReceived.Count > 0, TimeSpan.FromSeconds(2));

        Assert.That(nextReceived, Does.Contain("foo"));
    }

    [Test]
    public async Task Batching_by_size()
    {
        var items = new List<string>();

        Setup(2, x => items.AddRange(x));

        const string item1 = "foo";
        const string item2 = "bar";

        await Complete(item1, item2);

        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0], Is.EqualTo(item1));
        Assert.That(items[1], Is.EqualTo(item2));
    }

    [Test]
    public async Task Batching_by_time()
    {
        var items = new List<string>();
        var executed = new TaskCompletionSource();

        Setup(batchSize: 10, batchPeriod: TimeSpan.FromMilliseconds(10), async x =>
        {
            items.AddRange(x);
            executed.SetResult();
            await Task.CompletedTask;
        });

        await Send("foo");
        await executed.Task;

        Assert.That(items.Count, Is.EqualTo(1));
        await Complete();
    }

    [Test]
    public async Task Batching_respects_completion()
    {
        var items = new List<string[]>();
        Setup(2, x => items.Add(x));

        await Complete("foo", "bar", "buzz");

        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Length, Is.EqualTo(2));
        Assert.That(items[1].Length, Is.EqualTo(1));
    }

    [Test]
    public async Task Input_count()
    {
        var blocker = new TaskCompletionSource();
        var entered = new AutoResetEvent(false);

        Setup(async _ =>
        {
            entered.Set();
            await blocker.Task;
        }, batchSize: 2);

        await Send("1");
        await Send("2");

        entered.WaitOne();

        Assert.That(pipe.InputCount, Is.EqualTo(0));

        await Send("1");
        await Send("2");

        Assert.That(pipe.InputCount, Is.EqualTo(2));
    }

    async Task Complete(params string[] items)
    {
        await Send(items);
        await Complete();
    }

    async Task Send(params string[] items)
    {
        foreach (var item in items)
            await pipe.Send(item);
    }

    async Task Complete()
    {
        pipe.Complete();
        await pipe.Completion;
    }

    void Setup(int batchSize, TimeSpan batchPeriod, Func<string[], Task> action) =>
        pipe = Pipe<string>.Batch(batchSize, action).BatchTriggerPeriod(batchPeriod).ToPipe();

    void Setup(int batchSize, Action<string[]> action) =>
        pipe = Pipe<string>.Batch(batchSize, action).ToPipe();

    void Setup(Func<string, Task> action) =>
        pipe = Pipe<string>.Batch(1, items => action(items[0])).ToPipe();

    void Setup(Func<string[], Task> action, int batchSize, int? boundedCapacity = null) =>
        pipe = Pipe<string>.Batch(batchSize, action).BoundedCapacity(boundedCapacity).ToPipe();
}