namespace Simpipe.Pipes;

[TestFixture]
public class ForkPipeFixture
{
    class TestItem
    {
        public required string Block1Value;
        public required string Block2Value;
    }

    [Test]
    public async Task Fork_join()
    {
        var item = new TestItem { Block1Value = "", Block2Value = "" };
        var tcs1 = new TaskCompletionSource();
        var joinedItems = new List<TestItem>();

        var b1 = Parallel<TestItem>
            .Action(async i =>
            {
                i.Block1Value = "1";
                await tcs1.Task;
            })
            .Id("b1");

        var b2 = Parallel<TestItem>
            .Action(i => i.Block2Value = "2")
            .Id("b2");

        Pipe<TestItem> fork = Pipe<TestItem>
            .Fork(b1, b2)
            .Join(joinedItems.Add);

        await fork.Send(item);
        await Task.Delay(10);

        Assert.That(item.Block1Value, Is.EqualTo("1"));
        Assert.That(item.Block2Value, Is.EqualTo("2"));
        Assert.That(joinedItems, Has.Count.EqualTo(0));

        tcs1.SetResult();
        await Task.Delay(10);

        Assert.That(joinedItems, Has.Count.EqualTo(1));
        Assert.That(joinedItems[0], Is.SameAs(item));
    }

    [Test]
    public async Task Sends_next_when_all_blocks_complete()
    {
        var item = new TestItem { Block1Value = "", Block2Value = "" };
        var tcs1 = new TaskCompletionSource();

        var b1 = Parallel<TestItem>
            .Action(async i =>
            {
                i.Block1Value = "1";
                await tcs1.Task;
            })
            .Id("b1");

        var b2 = Parallel<TestItem>
            .Action(i => i.Block2Value = "2")
            .Id("b2");

        Pipe<TestItem> fork = Pipe<TestItem>
            .Fork(b1, b2);

        var nextReceived = new List<TestItem>();
        var nextPipe = PipeMock<TestItem>.Create(id: "next", nextReceived.Add);
        fork.LinkNext(nextPipe);

        await fork.Send(item);
        await Task.Delay(10);

        Assert.That(nextReceived, Has.Count.EqualTo(0));

        tcs1.SetResult();
        await Task.Delay(10);

        Assert.That(nextReceived[0], Is.SameAs(item));
    }
}