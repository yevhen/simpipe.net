namespace Simpipe.Blocks;

[TestFixture]
public class ParallelBlockFixture
{
    class TestItem
    {
        public required string Block1Value;
        public required string Block2Value;
    }

    [Test]
    public async Task Awaits_all_inner_blocks_before_reporting_done()
    {
        var item = new TestItem { Block1Value = "", Block2Value = "" };
        var doneItems = new List<TestItem>();
        var tcs1 = new TaskCompletionSource();

        var parallelBlock = new ParallelBlock<TestItem>(
            capacity: 1,
            blockCount: 2,
            done: item =>
            {
                doneItems.Add(item);
                return Task.CompletedTask;
            },
            innerDone => new()
            {
                ["b1"] = new ActionBlock<TestItem>(
                    capacity: 1,
                    parallelism: 1,
                    BlockItemAction<TestItem>.Async(async i =>
                    {
                        await Task.Delay(50);
                        i.Block1Value = "1";
                        await tcs1.Task;
                    }),
                    innerDone),

                ["b2"] = new ActionBlock<TestItem>(
                    capacity: 1,
                    parallelism: 1,
                    BlockItemAction<TestItem>.Sync(i => i.Block2Value = "2"),
                    innerDone)
            });

        await parallelBlock.Send(item);
        await Task.Delay(100);

        Assert.That(item.Block1Value, Is.EqualTo("1"));
        Assert.That(item.Block2Value, Is.EqualTo("2"));
        Assert.That(doneItems, Has.Count.EqualTo(0));

        tcs1.SetResult();
        await Task.Delay(10);

        Assert.That(doneItems, Has.Count.EqualTo(1));
        Assert.That(doneItems[0], Is.SameAs(item));
    }
}