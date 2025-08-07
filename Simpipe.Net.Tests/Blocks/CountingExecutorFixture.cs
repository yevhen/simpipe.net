using Simpipe.Blocks;

namespace Simpipe.Tests.Blocks;

public class CountingExecutorFixture
{
    static readonly BlockItem<int> Item = new([42, 50, 100]);

    [Test]
    public void Input_count_is_zero_on_creation()
    {
        var executor = new CountingExecutor<int>();
        Assert.That(executor.InputCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Input_count_increments_on_send()
    {
        var executor = new CountingExecutor<int>();

        await executor.ExecuteSend(Item, BlockItemAction<int>.Noop);

        Assert.That(executor.InputCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Input_count_decrements_on_action_execute()
    {
        var executor = new CountingExecutor<int>();

        await executor.ExecuteSend(Item, BlockItemAction<int>.Noop);
        await executor.ExecuteAction(Item, BlockItemAction<int>.Noop);

        Assert.That(executor.InputCount, Is.EqualTo(0));
    }

    [Test]
    public void Working_count_is_zero_on_creation()
    {
        var executor = new CountingExecutor<int>();
        Assert.That(executor.WorkingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Working_count_increments_on_action()
    {
        var executor = new CountingExecutor<int>();

        var workingCount = 0;
        await executor.ExecuteAction(Item, BlockItemAction<int>.BatchSync(_ => workingCount = executor.WorkingCount));

        Assert.That(workingCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Working_count_decrements_on_action_complete()
    {
        var executor = new CountingExecutor<int>();

        await executor.ExecuteAction(Item, BlockItemAction<int>.Noop);

        Assert.That(executor.WorkingCount, Is.EqualTo(0));
    }

    [Test]
    public void Output_count_is_zero_on_creation()
    {
        var executor = new CountingExecutor<int>();
        Assert.That(executor.OutputCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Output_count_increments_on_done()
    {
        var executor = new CountingExecutor<int>();

        var outputCount = 0;
        await executor.ExecuteDone(Item, BlockItemAction<int>.BatchSync(_ => outputCount = executor.OutputCount));

        Assert.That(outputCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Output_count_decrements_on_done_complete()
    {
        var executor = new CountingExecutor<int>();

        await executor.ExecuteDone(Item, BlockItemAction<int>.Noop);

        Assert.That(executor.OutputCount, Is.EqualTo(0));
    }
}