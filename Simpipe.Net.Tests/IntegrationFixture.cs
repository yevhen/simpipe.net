using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using NUnit.Framework;

namespace Youscan.Core.Pipes;

[TestFixture]
public partial class IntegrationFixture
{
    [Test]
    public async Task Links_tpl_block_to_pipe()
    {
        var targetBlock = new TransformBlock<TestItem, TestItem>(x =>
        {
            x.Data += "tpl";
            return x;
        });
        
        var builder = new PipeBuilder<TestItem>();
        var pipe = builder.Action(x => x.Data += "pipe").ToPipe();

        targetBlock.LinkTo(pipe.Target);
        
        var item = new TestItem();
        await targetBlock.SendAsync(item);

        await Complete(targetBlock);
        await Complete(pipe);

        Assert.AreEqual("tplpipe", item.Data);
    }

    [Test]
    public async Task Links_pipe_to_tpl_block()
    {
        var targetBlock = new TransformBlock<TestItem, TestItem>(x =>
        {
            x.Data += "tpl";
            return x;
        });
        
        var builder = new PipeBuilder<TestItem>();
        var pipe = builder.Action(x => x.Data += "pipe").ToPipe();

        pipe.LinkTo(targetBlock);
        targetBlock.LinkTo(DataflowBlock.NullTarget<TestItem>());
        
        var item = new TestItem();
        await pipe.Send(item);

        await Complete(pipe);
        await Complete(targetBlock);

        Assert.AreEqual("pipetpl", item.Data);
    }
    
    [Test]
    public async Task Links_pipe_to_tpl_block_with_condition()
    {
        var conditionalTarget = new TransformBlock<TestItem, TestItem>(x =>
        {
            x.Data += "tpl1";
            return x;
        });
        var defaultTarget = new TransformBlock<TestItem, TestItem>(x =>
        {
            x.Data += "tpl2";
            return x;
        });

        var builder = new PipeBuilder<TestItem>();
        var pipe = builder.Action(_ => { }).ToPipe();

        pipe.LinkTo(conditionalTarget, x => x.Data == "1");
        pipe.LinkTo(defaultTarget);
        conditionalTarget.LinkTo(DataflowBlock.NullTarget<TestItem>());
        defaultTarget.LinkTo(DataflowBlock.NullTarget<TestItem>());
        
        var item1 = new TestItem { Data = "1"};
        var item2 = new TestItem { Data = "2"};
        
        await pipe.Send(item1);
        await pipe.Send(item2);

        await Complete(pipe);
        await Complete(conditionalTarget);
        await Complete(defaultTarget);

        Assert.AreEqual("1tpl1", item1.Data);
        Assert.AreEqual("2tpl2", item2.Data);
    }

    [Test]
    public async Task Routing_block()
    {
        var firstBlockExecuted = false;
        var secondBlockExecuted = false;

        var source = new TransformBlock<TestItem, TestItem>(x => x);
        var first = new ActionBlock<TestItem>(x => firstBlockExecuted = true);
        var second = new ActionBlock<TestItem>(x => secondBlockExecuted = true);

        var router = new RoutingBlock<TestItem>(x =>
        {
            if (x.Data == "1") return first;
            if (x.Data == "2") return second;
            throw new InvalidOperationException();
        });
        
        source.LinkTo(router);

        await source.SendAsync(new TestItem{Data = "1"});
        SpinWait.SpinUntil(() => firstBlockExecuted || secondBlockExecuted, TimeSpan.FromSeconds(5));
        
        Assert.True(firstBlockExecuted);
        Assert.False(secondBlockExecuted);

        firstBlockExecuted = false;

        await source.SendAsync(new TestItem{Data = "2"});
        SpinWait.SpinUntil(() => firstBlockExecuted || secondBlockExecuted, TimeSpan.FromSeconds(5));
        
        Assert.False(firstBlockExecuted);
        Assert.True(secondBlockExecuted);
    }

    static async Task Complete(Pipe<TestItem> pipe)
    {
        pipe.Complete();
        await pipe.Completion;
    }

    static async Task Complete(IDataflowBlock block)
    {
        block.Complete();
        await block.Completion;
    }
}

public class TestItem
{
    public TestItem() => Data = "";

    public string Data { get; set; }
}