using System.Collections.Concurrent;
using System.Diagnostics;

namespace Simpipe.Pipes;

[TestFixture]
public class PipeFixture
{
    Pipe<TestItem> pipe = null!;
    Pipe<TestItem>? next;

    CancellationTokenSource cancellation = null!;

    [SetUp]
    public void SetUp() => cancellation = new CancellationTokenSource();

    [TearDown]
    public void TearDown()
    {
        cancellation.Dispose();
    }

    [Test]
    public async Task Sends_item_to_next_pipe_only_upon_completion_of_action()
    {
        var blocker = new TaskCompletionSource();

        var pipeExecuted = false;
        var nextPipeExecuted = false;

        Setup(async _ =>
        {
            pipeExecuted = true;
            await blocker.Task;
        });

        SetupNext(x =>
        {
            nextPipeExecuted = true;
            x.Data += "bar";
        });

        var item = CreateItem("foo");
        await Send(item);

        SpinWait.SpinUntil(() => pipeExecuted, TimeSpan.FromSeconds(3));
        Assert.That(item.Data, Is.EqualTo("foo"));
        Assert.False(nextPipeExecuted);

        blocker.SetResult();
        SpinWait.SpinUntil(() => nextPipeExecuted, TimeSpan.FromSeconds(2));

        Assert.That(item.Data, Is.EqualTo("foobar"));
    }

    [Test]
    public async Task Send_to_next_pipe_is_awaited()
    {
        var blocker = new TaskCompletionSource();

        Setup(_ => {});
        SetupNextAsync(async _ => await blocker.Task);

        var task = Complete(CreateItem());
        Assert.False(task.IsCompleted);

        blocker.SetResult();
        await task;

        Assert.True(task.IsCompleted);
    }

    [Test]
    public async Task SendNext_executes_next_pipe()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();
        var mainPipe = PipeMock<TestItem>.Create(mainProcessed.Add);
        var nextPipe = PipeMock<TestItem>.Create(nextProcessed.Add);
        mainPipe.LinkNext(nextPipe);

        var item = CreateItem();
        await mainPipe.SendNext(item);

        Assert.That(mainProcessed, Is.Empty);
        Assert.That(nextProcessed.Single(), Is.SameAs(item));
    }

    [Test]
    public void SendNext_sends_to_null_if_no_next()
    {
        var testPipe = PipeMock<TestItem>.Create();

        Assert.DoesNotThrowAsync(() => testPipe.SendNext(CreateItem()));
    }

    [Test]
    public async Task Sends_to_pipe_returned_by_routing_predicate()
    {
        var routedProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();

        var routedPipe = PipeMock<TestItem>.Create(routedProcessed.Add);
        var nextPipe = PipeMock<TestItem>.Create(nextProcessed.Add);
        var mainPipe = PipeMock<TestItem>.Create(_ => {}, route: _ => routedPipe);

        mainPipe.LinkNext(nextPipe);

        var item = CreateItem();
        await mainPipe.Send(item);

        Assert.That(nextProcessed, Is.Empty);
        Assert.That(routedProcessed.Single(), Is.SameAs(item));
    }

    [Test]
    public async Task Sends_to_next_if_route_does_not_match()
    {
        var nextProcessed = new List<TestItem>();
        var nextPipe = PipeMock<TestItem>.Create(nextProcessed.Add);
        var mainPipe = PipeMock<TestItem>.Create(_ => {}, route: _ => null);
        mainPipe.LinkNext(nextPipe);

        var item = CreateItem();
        await mainPipe.Send(item);

        Assert.That(nextProcessed.Single(), Is.SameAs(item));
    }

    [Test]
    public async Task Check_next_route_if_route_returns_null()
    {
        var pipe = PipeMock<TestItem>.Create();

        var routedReceived = new List<TestItem>();
        var routed = PipeMock<TestItem>.Create(routedReceived.Add);

        pipe.LinkTo(_ => null);
        pipe.LinkTo(_ => routed);

        var item = CreateItem();
        await pipe.Send(item);

        Assert.That(routedReceived, Has.Count.EqualTo(1));
        Assert.That(routedReceived.Single(), Is.SameAs(item));
    }

    [Test]
    public async Task Routes_match_in_order()
    {
        var pipe = PipeMock<TestItem>.Create();
        var routed1Received = new List<TestItem>();
        var routed2Received = new List<TestItem>();
        var routed1 = PipeMock<TestItem>.Create(routed1Received.Add);
        var routed2 = PipeMock<TestItem>.Create(routed2Received.Add);

        pipe.LinkTo(_ => routed1);
        pipe.LinkTo(_ => routed2);

        var item = CreateItem();
        await pipe.Send(item);

        Assert.That(routed1Received.Single(), Is.SameAs(item));
        Assert.That(routed2Received, Is.Empty);
    }

    [Test]
    public async Task Conditional_execution_when_filter_matches()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();

        var nextPipe = PipeMock<TestItem>.Create(nextProcessed.Add);
        var mainPipe = PipeMock<TestItem>.Create(
            action: x => mainProcessed.Add(x),
            filter: x => x.Data == "1");
        mainPipe.LinkNext(nextPipe);

        var item = CreateItem("1");
        await mainPipe.Send(item);

        Assert.That(mainProcessed, Has.Count.EqualTo(1));
        Assert.That(nextProcessed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Conditional_execution_when_filter_does_not_match()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();

        var nextPipe = PipeMock<TestItem>.Create(nextProcessed.Add);
        var mainPipe = PipeMock<TestItem>.Create(
            action: x => mainProcessed.Add(x),
            filter: x => x.Data == "1");        // Filter expects "1"
        mainPipe.LinkNext(nextPipe);

        var item = CreateItem("2");             // Send "2" - doesn't match filter
        await mainPipe.Send(item);

        Assert.That(mainProcessed, Is.Empty);  // Action skipped
        Assert.That(nextProcessed, Has.Count.EqualTo(1));  // Item still forwarded
    }

    [Test]
    public async Task Recursive_conditional_filtering_via_Send()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();
        var endProcessed = new List<TestItem>();

        var endPipe = PipeMock<TestItem>.Create(endProcessed.Add);
        var nextPipe = PipeMock<TestItem>.Create(
            action: x => nextProcessed.Add(x),
            filter: x => x.Data == "1");
        var mainPipe = PipeMock<TestItem>.Create(
            action: x => mainProcessed.Add(x),
            filter: x => x.Data == "1");

        mainPipe.LinkNext(nextPipe);
        nextPipe.LinkNext(endPipe);

        var item = CreateItem("2");
        await mainPipe.Send(item);

        Assert.That(mainProcessed, Is.Empty);  // Filtered out
        Assert.That(nextProcessed, Is.Empty);  // Filtered out
        Assert.That(endProcessed, Has.Count.EqualTo(1));   // Final destination
    }

    [Test]
    public async Task Recursive_conditional_filtering_via_link()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();
        var endProcessed = new List<TestItem>();

        var endPipe = PipeMock<TestItem>.Create(action: x => endProcessed.Add(x));
        var nextPipe = PipeMock<TestItem>.Create(
            action: x => nextProcessed.Add(x),
            filter: x => x.Data == "1");
        var mainPipe = PipeMock<TestItem>.Create(action: x => mainProcessed.Add(x));

        mainPipe.LinkNext(nextPipe);
        nextPipe.LinkNext(endPipe);

        var item = CreateItem("2");
        await mainPipe.Send(item);

        Assert.That(mainProcessed, Has.Count.EqualTo(1));  // Main pipe executed (no filter)
        Assert.That(nextProcessed, Is.Empty);              // Next pipe filtered out
        Assert.That(endProcessed, Has.Count.EqualTo(1));   // End pipe received cascaded item
    }

    [Test]
    public async Task Routes_are_only_used_for_items_that_passed_through_block()
    {
        var mainProcessed = new List<TestItem>();
        var nextProcessed = new List<TestItem>();
        var routedProcessed = new List<TestItem>();

        var routedPipe = PipeMock<TestItem>.Create(routedProcessed.Add);
        var nextPipe = PipeMock<TestItem>.Create(
            action: x => nextProcessed.Add(x),
            filter: x => x.Data == "1",
            route: _ => routedPipe);
        var mainPipe = PipeMock<TestItem>.Create(
            action: x => mainProcessed.Add(x),
            filter: x => x.Data == "1");

        mainPipe.LinkNext(nextPipe);

        var item = CreateItem("2");
        await mainPipe.Send(item);

        Assert.That(mainProcessed, Is.Empty);
        Assert.That(nextProcessed, Is.Empty);
        Assert.That(routedProcessed, Is.Empty, "Should not use route if didn't pass through the block");
    }

    [Test]
    public async Task Cancellation_does_not_send_completed_item_to_next_pipe()
    {
        var blocker = new TaskCompletionSource();

        var pipeExecuted = false;
        var nextPipeExecuted = false;

        Setup(async _ =>
        {
            pipeExecuted = true;
            await blocker.Task;
        });
        SetupNext(_ => nextPipeExecuted = true);

        await Send(CreateItem());
        SpinWait.SpinUntil(() => pipeExecuted, TimeSpan.FromSeconds(3));

        Cancel();
        blocker.SetResult();

        await Complete();

        Assert.False(nextPipeExecuted);
    }

    [Test]
    public async Task Integration_test()
    {
        var received1 = new List<int>();
        var received2 = new List<int>();

        Pipe<int> p1 = Pipe<int>.Action(x =>
        {
            Thread.Sleep(1);
            received1.Add(x);
        });

        Action<int[]> action = x =>
        {
            Thread.Sleep(1);
            received2.Add(x[0]);
        };
        Pipe<int> p2 = Pipe<int>.Batch(1, action);

        p1.LinkNext(p2);

        var queue = new BlockingCollection<int>();
        var sender = Task.Run(SendItems);

        var items = Enumerable.Range(0, 100).ToArray();
        foreach (var item in items)
        {
            Console.WriteLine($"Adding: {item}");
            await Task.Delay(1);
            queue.Add(item);
        }

        queue.CompleteAdding();
        await sender;

        WaitCompletion(p1);
        WaitCompletion(p2);

        Assert.That(received1, Is.EqualTo(items));
        Assert.That(received2, Is.EqualTo(items));

        async Task SendItems()
        {
            foreach (var item in queue.GetConsumingEnumerable())
            {
                Console.WriteLine($"Sending: {item}");
                await Task.Delay(1);
                await p1.Send(item);
            }
        }
    }

    static void WaitCompletion<T>(Pipe<T> pipe)
    {
        pipe.Complete();
        pipe.Completion.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    static Pipe<TestItem> CreatePipe(ActionPipeBuilder<TestItem> builder) => builder.ToPipe();
    static TestItem CreateItem(string? data = null) => new() { Data = data };

    void Setup(Func<TestItem, Task> action)
    {
        var options = Pipe<TestItem>.Action(action)
            .CancellationToken(cancellation.Token);

        pipe = CreatePipe(options);
    }

    void Setup(Action<TestItem> action)
    {
        var options = Pipe<TestItem>.Action(action)
            .CancellationToken(cancellation.Token);

        pipe = CreatePipe(options);
    }

    void SetupNext(Action<TestItem>? action = null, Func<TestItem, bool>? filter = null, Func<TestItem, Pipe<TestItem>>? route = null, Pipe<TestItem>? afterNext = null)
    {
        SetupNextAsync(x => {
            Debug.Assert(action != null, nameof(action) + " != null");
            action(x);
            return Task.CompletedTask;
        }, filter, route, afterNext);
    }

    void SetupNextAsync(Func<TestItem, Task>? action = null, Func<TestItem, bool>? filter = null, Func<TestItem, Pipe<TestItem>>? route = null, Pipe<TestItem>? afterNext = null)
    {
        var options = Pipe<TestItem>.Action(action ?? (_ => Task.CompletedTask));
        if (filter != null)
            options = options.Filter(filter);

        if (route != null)
            options = options.Route(route);

        next = options.ToPipe();
        pipe.LinkNext(next);

        if (afterNext != null)
            next.LinkNext(afterNext);
    }

    void Cancel() => cancellation.Cancel();

    async Task Complete(params TestItem[] items)
    {
        await Send(items);
        await Complete();
    }

    async Task Complete()
    {
        pipe.Complete();
        await pipe.Completion;

        if (next != null)
        {
            next.Complete();
            await next.Completion;
        }
    }

    async Task Send(params TestItem[] items)
    {
        foreach (var item in items)
            await pipe.Send(item);
    }

    class TestItem
    {
        public string? Data;
    }
}