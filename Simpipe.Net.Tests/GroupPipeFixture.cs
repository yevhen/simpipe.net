using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using FluentAssertions;

using NUnit.Framework;

namespace Youscan.Core.Pipes;

[TestFixture]
public class GroupPipeFixture
{
    [Test]
    public async Task Completes_every_inner_pipe()
    {
        var first = new PipeMock("foo1");
        var second = new PipeMock("foo2");

        var group = new GroupPipe<int>(new GroupPipeOptions<int>());
        group.Add(first);
        group.Add(second);

        group.Complete();
        Assert.False(first.CompleteExecuted);
        Assert.False(second.CompleteExecuted);

        first.ResolveCompletion();
        second.ResolveCompletion();

        await group.Completion;
        Assert.That(first.Completion.IsCompleted);
        Assert.That(second.Completion.IsCompleted);
    }

    [Test]
    public async Task Sends_to_matching_pipe()
    {
        var first = new PipeMock("foo1");
        var second = new PipeMock("foo2");

        const int firstItem = 42;
        const int secondItem = 100;

        var group = new GroupPipe<int>(new GroupPipeOptions<int>());

        group.Add(first, (item, _) => item == firstItem);
        group.Add(second, (item, _) => item == secondItem);

        await group.Send(firstItem);
        await group.Send(secondItem);

        first.ResolveCompletion();
        second.ResolveCompletion();

        group.Complete();
        await group.Completion;

        first.Received.Should().BeEquivalentTo(new[] { firstItem });
        second.Received.Should().BeEquivalentTo(new[] { secondItem });
    }

    [Test]
    public async Task Respects_filter()
    {
        var before = new ActionPipe<int>(new ActionPipeOptions<int>(PipeAction<int>.None()));
        var after = new PipeMock("bar");
        var inner = new PipeMock("foo");

        var group = new GroupPipe<int>(new GroupPipeOptions<int>()
            .Filter(x => x == 2));

        before.LinkTo(group);
        group.LinkTo(after);
        group.Add(inner);

        await before.Send(1);
        await before.Send(2);

        inner.ResolveCompletion();
        after.ResolveCompletion();

        before.Complete();
        await before.Completion;

        group.Complete();
        await group.Completion;

        inner.Received.Should().BeEquivalentTo(new[] { 2 });
        after.Received.Should().BeEquivalentTo(new[] { 1 });
    }

    [Test]
    public async Task Inner_next_is_linked_to_group_next_on_linking()
    {
        int? received = null;

        var inner = new ActionPipeOptions<int>(PipeAction<int>.For((int _) => {})).Id("inner").ToPipe();
        var next = new ActionPipeOptions<int>(PipeAction<int>.For(x => received = x)).Id("next").ToPipe();

        const int item = 42;

        var group = new GroupPipe<int>(new GroupPipeOptions<int>());

        group.Add(inner, (_, _) => true);
        group.LinkTo(next);

        await group.Send(item);

        group.Complete();
        await group.Completion;

        next.Complete();
        await next.Completion;

        Assert.That(received, Is.EqualTo(item));
    }
}