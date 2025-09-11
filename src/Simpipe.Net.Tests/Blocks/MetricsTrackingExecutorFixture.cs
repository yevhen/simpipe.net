using static SharpAssert.Sharp;

namespace Simpipe.Blocks;

public class MetricsTrackingExecutorFixture
{
    static readonly BlockItem<int> Item = new([42, 50, 100]);

    [Test]
    public void Has_input_count_zero_on_creation()
    {
        var executor = new MetricsTrackingExecutor<int>();
        Assert(executor.InputCount == 0);
    }

    [Test]
    public async Task Increments_input_count_on_send()
    {
        var executor = new MetricsTrackingExecutor<int>();

        await executor.ExecuteSend(Item, BlockItemAction<int>.Noop);

        Assert(executor.InputCount == 3);
    }

    [Test]
    public async Task Decrements_input_count_on_action_execute()
    {
        var executor = new MetricsTrackingExecutor<int>();

        await executor.ExecuteSend(Item, BlockItemAction<int>.Noop);
        await executor.ExecuteAction(Item, BlockItemAction<int>.Noop);

        Assert(executor.InputCount == 0);
    }

    [Test]
    public void Has_working_count_zero_on_creation()
    {
        var executor = new MetricsTrackingExecutor<int>();
        Assert(executor.WorkingCount == 0);
    }

    [Test]
    public async Task Increments_working_count_on_action()
    {
        var executor = new MetricsTrackingExecutor<int>();

        var workingCount = 0;
        await executor.ExecuteAction(Item, BlockItemAction<int>.BatchSync(_ => workingCount = executor.WorkingCount));

        Assert(workingCount == 3);
    }

    [Test]
    public async Task Decrements_working_count_on_action_complete()
    {
        var executor = new MetricsTrackingExecutor<int>();

        await executor.ExecuteAction(Item, BlockItemAction<int>.Noop);

        Assert(executor.WorkingCount == 0);
    }

    [Test]
    public void Has_output_count_zero_on_creation()
    {
        var executor = new MetricsTrackingExecutor<int>();
        Assert(executor.OutputCount == 0);
    }

    [Test]
    public async Task Increments_output_count_on_done()
    {
        var executor = new MetricsTrackingExecutor<int>();

        var outputCount = 0;
        await executor.ExecuteDone(Item, BlockItemAction<int>.BatchSync(_ => outputCount = executor.OutputCount));

        Assert(outputCount == 3);
    }

    [Test]
    public async Task Decrements_output_count_on_done_complete()
    {
        var executor = new MetricsTrackingExecutor<int>();

        await executor.ExecuteDone(Item, BlockItemAction<int>.Noop);

        Assert(executor.OutputCount == 0);
    }
}
