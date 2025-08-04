using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Youscan.Core.Pipes;

[TestFixture]
public class DynamicBatchPipeFixture
{
    DynamicBatchPipe<string> block = null!;
        
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
    public async Task Completion_does_pushes_to_next()
    {
        Setup(async _ =>
        {
            await Task.Delay(100);
        });

        var next = new PipeMock<string>("next");
        block.LinkTo(next);

        await Complete("foo");
        SpinWait.SpinUntil(() => next.Received.Count > 0, TimeSpan.FromSeconds(2));

        Assert.That(next.Received.Contains("foo"));
    }

    [Test]
    public async Task Batching_by_size()
    {
        var items = new List<string>();

        Setup(x => items.AddRange(x));

        const string item1 = "foo";
        const string item2 = "bar";

        await Complete(item1, item2);
        
        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(item1, items[0]);
        Assert.AreEqual(item2, items[1]);
    }

    [Test]
    public async Task Batching_by_time()
    {
        var items = new List<string>();
        var executed = new TaskCompletionSource();
            
        Setup(maxBatchSize: 2, maxBatchInterval: TimeSpan.FromMilliseconds(10), async x =>
        {
            items.AddRange(x);
            executed.SetResult();
            await Task.CompletedTask;
        });

        await Send("foo");
        await executed.Task;
            
        Assert.AreEqual(1, items.Count);
        await Complete();
    }

    [Test]
    public async Task Batching_by_time_when_first_empty()
    {
        var items = new List<string>();
        var executed = new TaskCompletionSource();

        var maxBatchInterval = TimeSpan.FromMilliseconds(10);
        Setup(maxBatchSize: 2, maxBatchInterval, async x =>
        {
            items.AddRange(x);
            executed.SetResult();
            await Task.CompletedTask;
        });

        await Task.Delay(maxBatchInterval * 10);

        await Send("foo");
        await executed.Task;
            
        Assert.AreEqual(1, items.Count);
        await Complete();
    }

    [Test]
    public void Batching_by_time_respects_last_batch_time()
    {
        var now = DateTime.Now;
        var batchTriggerPeriod = TimeSpan.FromSeconds(10);

        var lastBatchTimeEqualToBatchTriggerPeriod = now - batchTriggerPeriod;
        var lastBatchTimeLessThanBatchTriggerPeriod = now - batchTriggerPeriod / 2;
        var lastBatchTimeGreaterThanBatchTriggerPeriod = now - batchTriggerPeriod + TimeSpan.FromSeconds(1);
            
        Assert.True(BatchPipe<int>.ShouldTriggerBatch(now, lastBatchTimeEqualToBatchTriggerPeriod, batchTriggerPeriod));
        Assert.False(BatchPipe<int>.ShouldTriggerBatch(now, lastBatchTimeLessThanBatchTriggerPeriod, batchTriggerPeriod));
        Assert.False(BatchPipe<int>.ShouldTriggerBatch(now, lastBatchTimeGreaterThanBatchTriggerPeriod, batchTriggerPeriod));
    }

    [Test]
    public async Task Batching_respects_completion()
    {
        var items = new List<string[]>();
        Setup(maxBatchSize: 2, x => items.Add(x));

        await Complete("foo", "bar", "buzz");
        
        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(2, items[0].Length);
        Assert.AreEqual(1, items[1].Length);
    }

    async Task Complete(params string[] items)
    {
        await Send(items);
        await Complete();
    }

    async Task Send(params string[] items)
    {
        foreach (var item in items)
            await block.Send(item);
    }

    async Task Complete()
    {
        block.Complete();
        await block.Completion;
    }

    void Setup(int maxBatchSize, TimeSpan maxBatchInterval, Func<string[], Task> action) => 
        block = Builder.DynamicBatch(action)
            .MaxBatchSize(maxBatchSize)
            .MaxBatchInterval(maxBatchInterval).ToPipe();

    void Setup(Action<string[]> action) => 
        block = Builder.DynamicBatch(action)
            .DisableBatchingByTime().ToPipe();

    void Setup(int maxBatchSize, Action<string[]> action) => 
        block = Builder.DynamicBatch(action)
            .MaxBatchSize(maxBatchSize)
            .DisableBatchingByTime().ToPipe();
    
    void Setup(Func<string[], Task> action) => 
        block = Builder.DynamicBatch(action)
            .DisableBatchingByTime().ToPipe();

    PipeBuilder<string> Builder { get; } = new();
}

static class DynamicPipeBatchBlocksExtensions
{
    public static DynamicBatchPipeOptions<T> DisableBatchingByTime<T>(this DynamicBatchPipeOptions<T> options)
    {
        // makes batching by time ineffective to not interfere with testing of other functionality
        return options.InitialBatchInterval(TimeSpan.FromHours(1));
    }
}
