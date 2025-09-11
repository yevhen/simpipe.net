using static SharpAssert.Sharp;

namespace Simpipe.Pipes;

[TestFixture]
public class ActionPipeFixture
{
    Pipe<int> pipe = null!;

    [Test]
    public async Task Executes_action()
    {
        var executed = false;
        Setup(async _ =>
        {
            await Task.Delay(1);
            executed = true;
        });

        await Complete(42);

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

        Assert(await ThrowsAsync<ArgumentException>(() => Complete(42)));
    }

    [Test]
    public async Task Pushes_to_next_on_completion()
    {
        Setup(async _ =>
        {
            await Task.Delay(100);
        });

        var nextReceived = new List<int>();
        var nextPipe = PipeMock<int>.Create(id: "next", nextReceived.Add);
        pipe.LinkNext(nextPipe);

        await Complete(42);
        SpinWait.SpinUntil(() => nextReceived.Count > 0, TimeSpan.FromSeconds(2));

        Assert(nextReceived.Contains(42));
    }

    async Task Complete(params int[] items)
    {
        await Send(items);
        await Complete();
    }

    async Task Send(params int[] items)
    {
        foreach (var item in items)
            await pipe.Send(item);
    }

    async Task Complete()
    {
        pipe.Complete();
        await pipe.Completion;
    }

    void Setup(Func<int, Task> action) =>
        pipe = Pipe<int>.Action(action).ToPipe();
}
