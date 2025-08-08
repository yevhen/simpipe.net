namespace Simpipe.Blocks;

[TestFixture]
public class ParallelBlockFixture
{
    class TestItem
    {
        public int Id = 0;
        public string Block1Value;
        public string Block2Value;
    }

    class CompletionTrackingExecutor<T> : IActionBlockExecutor<T>
        where T : notnull
    {
        readonly ActionBlock<T> completion;
        readonly Dictionary<T, int> completed = new();
        readonly int count;
        readonly Func<T, Task> done;
        readonly IActionBlockExecutor<T> executor;

        public CompletionTrackingExecutor(int count, Func<T, Task> done, IActionBlockExecutor<T> executor)
        {
            this.count = count;
            this.done = done;
            this.executor = executor;

            completion = new ActionBlock<T>(capacity: 1, parallelism: 1, BlockItemAction<T>.Async(TrackDone));
        }

        async Task TrackDone(T item)
        {
            if (completed.TryGetValue(item, out var currentCount))
                completed[item] = currentCount + 1;
            else
                completed[item] = 1;

            if (completed[item] == count)
                await done(item);
        }

        public Task ExecuteSend(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> send) =>
             executor.ExecuteSend(block, item, send);

        public Task ExecuteAction(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> action) =>
            executor.ExecuteAction(block, item, action);

        public async Task ExecuteDone(IActionBlock<T> block, BlockItem<T> item, BlockItemAction<T> done)
        {
            await executor.ExecuteDone(block, item, done);
            await completion.Send(item);
        }
    }

    [Test]
    public async Task Awaits_all_inner_blocks_before_reporting_done()
    {
        var item = new TestItem { Block1Value = "", Block2Value = "" };

        var doneItems = new List<TestItem>();

        var executor = new CompletionTrackingExecutor<TestItem>(
            count: 2,
            done: x =>
            {
                doneItems.Add(x);
                return Task.CompletedTask;
            },
            DefaultExecutor<TestItem>.Instance);

        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        var innerBlock1 = new ActionBlock<TestItem>(
            capacity: 1,
            parallelism: 1,
            BlockItemAction<TestItem>.Async(i =>
            {
                i.Block1Value = "1";
                return tcs1.Task;
            }),
            executor: executor);

        var innerBlock2 = new ActionBlock<TestItem>(
            capacity: 1,
            parallelism: 1,
            BlockItemAction<TestItem>.Async(i =>
            {
                i.Block2Value = "2";
                return tcs2.Task;
            }),
            executor: executor);

        await innerBlock1.Send(item);
        await innerBlock2.Send(item);

        await Task.Delay(10);

        tcs1.SetResult();
        tcs2.SetResult();

        Assert.That(item.Block1Value, Is.EqualTo("1"));
        Assert.That(item.Block2Value, Is.EqualTo("2"));
        Assert.That(doneItems, Has.Count.EqualTo(1));
        Assert.That(doneItems[0], Is.SameAs(item));
    }
}