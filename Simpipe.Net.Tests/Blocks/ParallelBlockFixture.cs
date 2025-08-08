namespace Simpipe.Blocks;

[TestFixture]
public class ParallelBlockFixture
{
    class TestItem
    {
        public string Block1Value;
        public string Block2Value;
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
            executor: DefaultExecutor<TestItem>.Instance,
            done: item =>
            {
                doneItems.Add(item);
                return Task.CompletedTask;
            },
            executor => new()
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
                    executor: executor),

                ["b2"] = new ActionBlock<TestItem>(
                    capacity: 1,
                    parallelism: 1,
                    BlockItemAction<TestItem>.Sync(i => i.Block2Value = "2"),
                    executor: executor)
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

    [Test]
    public async Task Inner_blocks_counters()
    {
        var item = new TestItem();
        var tcs = new TaskCompletionSource();

        var parallelBlock = new ParallelBlock<TestItem>(
            capacity: 1,
            blockCount: 2,
            executor: DefaultExecutor<TestItem>.Instance,
            done: _ => Task.CompletedTask,
            executor => new()
            {
                ["b1"] = new ActionBlock<TestItem>(
                    capacity: 1,
                    parallelism: 1,
                    BlockItemAction<TestItem>.Async(_ => tcs.Task),
                    executor: executor),

                ["b2"] = new ActionBlock<TestItem>(
                    capacity: 1,
                    parallelism: 1,
                    BlockItemAction<TestItem>.Async(_ => tcs.Task),
                    executor: executor)
            });

        Assert.That(parallelBlock.GetCounter("b1").WorkingCount, Is.EqualTo(0));
        Assert.That(parallelBlock.GetCounter("b2").WorkingCount, Is.EqualTo(0));

        await parallelBlock.Send(item);
        await Task.Delay(10);

        Assert.That(parallelBlock.GetCounter("b1").WorkingCount, Is.EqualTo(1));
        Assert.That(parallelBlock.GetCounter("b2").WorkingCount, Is.EqualTo(1));

        tcs.SetResult();
        await Task.Delay(10);

        Assert.That(parallelBlock.GetCounter("b1").WorkingCount, Is.EqualTo(0));
        Assert.That(parallelBlock.GetCounter("b2").WorkingCount, Is.EqualTo(0));
    }
}