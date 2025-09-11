using Simpipe.Blocks;
using static SharpAssert.Sharp;

namespace Simpipe.Pipes;

[TestFixture]
public class ForkPipeFixture
{
    class TestItem
    {
        public string Text = "";
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

        Assert(item.Block1Value == "1");
        Assert(item.Block2Value == "2");
        Assert(joinedItems.Count == 0);

        tcs1.SetResult();
        await Task.Delay(10);

        Assert(joinedItems.Count == 1);
        Assert(ReferenceEquals(joinedItems[0], item));
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

        Assert(nextReceived.Count == 0);

        tcs1.SetResult();
        await Task.Delay(10);

        Assert(ReferenceEquals(nextReceived[0], item));
    }

    [Test]
    public async Task Parallel_action_block_filter()
    {
        var item = new TestItem { Text = "test", Block1Value = "", Block2Value = "" };

        IParallelBlockBuilder<TestItem> builder = new ParallelActionBlockBuilder<TestItem>(
            BlockItemAction<TestItem>.Sync(x => x.Block1Value = "foo"))
            .Filter(x => x.Text != "test");

        var doneItems = new List<TestItem>();
        var block = builder.ToBlock(BlockItemAction<TestItem>.Sync(doneItems.Add));

        await block.Send(item);
        await block.Complete();

        Assert(doneItems.Count == 1);
        Assert(ReferenceEquals(doneItems[0], item));
        Assert(item.Block1Value == "");
    }

    [Test]
    public async Task Parallel_batch_action_block_filter()
    {
        var item = new TestItem { Text = "test", Block1Value = "", Block2Value = "" };

        IParallelBlockBuilder<TestItem> builder = new ParallelBatchActionBlockBuilder<TestItem>(
            batchSize: 1,
            BlockItemAction<TestItem>.BatchSync(x => x[0].Block1Value = "foo"))
            .Filter(x => x.Text != "test");

        var doneItems = new List<TestItem>();
        var block = builder.ToBlock(BlockItemAction<TestItem>.Sync(doneItems.Add));

        await block.Send(item);
        await block.Complete();

        Assert(doneItems.Count == 1);
        Assert(ReferenceEquals(doneItems[0], item));
        Assert(item.Block1Value == "");
    }
}