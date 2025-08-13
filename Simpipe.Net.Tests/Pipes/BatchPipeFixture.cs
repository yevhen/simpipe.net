using static SharpAssert.Sharp;

namespace Simpipe.Pipes;

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

        Assert(executed);
    }

    [Test]
    public async Task Awaits_completion()
    {
        Setup(async _ =>
        {
            await Task.Delay(1);
            throw new ArgumentException();
        });

        Assert(await ThrowsAsync<ArgumentException>(() => Complete("boom")));
    }

    [Test]
    public async Task Pushes_to_next_on_completion()
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

        Assert(nextReceived.Contains("foo"));
    }

    [Test]
    public async Task Batches_by_size()
    {
        var items = new List<string>();

        Setup(2, x => items.AddRange(x));

        const string item1 = "foo";
        const string item2 = "bar";

        await Complete(item1, item2);

        Assert(items.Count == 2);
        Assert(items[0] == item1);
        Assert(items[1] == item2);
    }

    [Test]
    public async Task Batches_by_time()
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

        Assert(items.Count == 1);
        await Complete();
    }

    [Test]
    public async Task Respects_completion_when_batching()
    {
        var items = new List<string[]>();
        Setup(2, x => items.Add(x));

        await Complete("foo", "bar", "buzz");

        Assert(items.Count == 2);
        Assert(items[0].Length == 2);
        Assert(items[1].Length == 1);
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
}
