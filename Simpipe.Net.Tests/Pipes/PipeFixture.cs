using System.Collections.Concurrent;
using System.Diagnostics;
using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes
{
    [TestFixture]
    public class PipeFixture
    {
        readonly PipeBuilder<TestItem> builder = new();

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
            TestItem? pipeReceived = null;
            TestItem? nextPipeReceived = null;

            Setup(x => pipeReceived = x);
            SetupNext(x => nextPipeReceived = x);

            var item = CreateItem();
            await pipe.SendNext(item);

            await Complete();

            Assert.IsNull(pipeReceived);
            Assert.That(nextPipeReceived, Is.SameAs(item));
        }

        [Test]
        public void SendNext_sends_to_null_if_no_next()
        {
            Setup(_ => {});

            Assert.DoesNotThrowAsync(() => pipe.SendNext(CreateItem()));
        }

        [Test]
        public async Task Sends_to_pipe_returned_by_routing_predicate()
        {
            TestItem? routedPipeReceived = null;
            TestItem? nextPipeReceived = null;

            var routed = CreatePipe(x => routedPipeReceived = x);
            Setup(_ => routed, _ => {});
            SetupNext(x => nextPipeReceived = x);

            var item = CreateItem();
            await Complete(item);

            routed.Complete();
            await routed.Completion;

            Assert.Null(nextPipeReceived);
            Assert.That(routedPipeReceived, Is.SameAs(item));
        }

        [Test]
        public async Task Sends_to_next_if_route_does_not_match()
        {
            TestItem? nextPipeReceived = null;

            Setup(_ => null!, _ => {});
            SetupNext(x => nextPipeReceived = x);

            var item = CreateItem();
            await Complete(item);

            Assert.That(nextPipeReceived, Is.SameAs(item));
        }

        [Test]
        public async Task Check_next_route_if_route_returns_null()
        {
            Setup(_ => { });

            TestItem? routedPipeReceived = null;
            var routed = CreatePipe(x => routedPipeReceived = x);
            
            pipe.LinkTo(_ => null);
            pipe.LinkTo(_ => routed);

            var item = CreateItem();
            await Complete(item);

            routed.Complete();
            await routed.Completion;
            
            Assert.That(routedPipeReceived, Is.SameAs(item));
        }
        
        [Test]
        public async Task Routes_match_in_order()
        {
            Setup(_ => { });

            TestItem? routed1PipeReceived = null;
            TestItem? routed2PipeReceived = null;

            var routed1 = CreatePipe(x => routed1PipeReceived = x);
            var routed2 = CreatePipe(x => routed2PipeReceived = x);
            
            pipe.LinkTo(_ => routed1);
            pipe.LinkTo(_ => routed2);

            var item = CreateItem();
            await Complete(item);

            routed1.Complete();
            await routed1.Completion;

            routed2.Complete();
            await routed2.Completion;

            Assert.That(routed1PipeReceived, Is.SameAs(item));
            Assert.Null(routed2PipeReceived);
        }

        [Test] 
        public async Task Conditional_execution_when_filter_matches()
        {
            var pipeExecuted = false;
            var nextPipeExecuted = false;
        
            Setup(_ => pipeExecuted = true, a => a.Data == "1");
            SetupNext(_ => nextPipeExecuted = true);
        
            var item = CreateItem("1");
            await Complete(item);
        
            Assert.True(pipeExecuted);
            Assert.True(nextPipeExecuted);
        }

        [Test] 
        public async Task Conditional_execution_when_filter_does_not_match()
        {
            var pipeExecuted = false;
            var nextPipeExecuted = false;
        
            Setup(_ => pipeExecuted = true, a => a.Data == "1");
            SetupNext(_ => nextPipeExecuted = true);
        
            var item = CreateItem("2");
            await Complete(item);
        
            Assert.False(pipeExecuted);
            Assert.True(nextPipeExecuted);
        }

        [Test] 
        public async Task Recursive_conditional_filtering_via_Send()
        {
            var pipeExecuted = false;
            var nextPipeExecuted = false;
            var endPipeExecuted = false;

            var endPipe = CreatePipe(_ => endPipeExecuted = true);

            Setup(_ => pipeExecuted = true, a => a.Data == "1");
            SetupNext(_ => nextPipeExecuted = true, a => a.Data == "1", afterNext: endPipe);
        
            var item = CreateItem("2");
            await Complete(item);

            endPipe.Complete();
            await endPipe.Completion;
        
            Assert.False(pipeExecuted);
            Assert.False(nextPipeExecuted);
            Assert.True(endPipeExecuted);
        }

        [Test] 
        public async Task Recursive_conditional_filtering_via_link()
        {
            var pipeExecuted = false;
            var nextPipeExecuted = false;
            var endPipeExecuted = false;

            var endPipe = CreatePipe(_ => endPipeExecuted = true);

            Setup(_ => pipeExecuted = true);
            SetupNext(_ => nextPipeExecuted = true, a => a.Data == "1", afterNext: endPipe);
        
            var item = CreateItem("2");
            await Complete(item);

            endPipe.Complete();
            await endPipe.Completion;
        
            Assert.True(pipeExecuted);
            Assert.False(nextPipeExecuted);
            Assert.True(endPipeExecuted);
        }

        [Test] 
        public async Task Routes_are_only_used_for_items_that_passed_through_block()
        {
            var pipeExecuted = false;
            var nextPipeExecuted = false;

            TestItem? routedPipeReceived = null;
            var routed = CreatePipe(x => routedPipeReceived = x);

            Setup(_ => pipeExecuted = true, a => a.Data == "1");
            SetupNext(_ => nextPipeExecuted = true, a => a.Data == "1", route: _ => routed);
        
            var item = CreateItem("2");
            await Complete(item);

            routed.Complete();
            await routed.Completion;
        
            Assert.False(pipeExecuted);
            Assert.False(nextPipeExecuted);
            Assert.Null(routedPipeReceived, "Should not use route if didn't pass through the block");
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
        public async Task Working_count_for_single_value()
        {
            var blocker = new TaskCompletionSource();
            Setup(_ => blocker.Task);

            var t = Send(CreateItem());
            AssertWorkingCount(1);

            blocker.SetResult();
            await t;
            AssertWorkingCount(0);
        }

        [Test]
        public async Task Integration_test()
        {
            var b = new PipeBuilder<int>();

            var received1 = new List<int>();
            var received2 = new List<int>();
            
            Pipe<int> p1 = b.Action(x =>
            {
                Thread.Sleep(1);
                received1.Add(x);
            });
            
            Pipe<int> p2 = b.Batch(1, x =>
            {
                Thread.Sleep(1);
                received2.Add(x[0]);
            });
            
            p1.LinkTo(p2);
            
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

        static void WaitCompletion<T>(IPipe<T> pipe)
        {
            pipe.Complete();
            pipe.Completion.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static Pipe<TestItem> CreatePipe(ActionPipeOptions<TestItem> options) => ActionPipe<TestItem>.Create(options);

        Pipe<TestItem> CreatePipe(Action<TestItem> action) => CreatePipe(x =>
        {
            action(x);
            return Task.CompletedTask;
        });

        Pipe<TestItem> CreatePipe(Func<TestItem, Task> action) => builder.Action(action);

        static TestItem CreateItem(string? data = null) => new() { Data = data };

        void Setup(Func<TestItem, Task> action)
        {
            var options = builder.Action(action)
                .CancellationToken(cancellation.Token);
            
            pipe = CreatePipe(options); 
        }       

        void Setup(Action<TestItem> action)
        {
            var options = builder.Action(action)
                .CancellationToken(cancellation.Token);
            
            pipe = CreatePipe(options); 
        }

        void Setup(Action<TestItem> action, Func<TestItem, bool> filter)
        {
            var options = builder.Action(action)
                .Filter(filter)
                .CancellationToken(cancellation.Token);

            pipe = CreatePipe(options);
        }

        void Setup(Func<TestItem, IPipe<TestItem>> route, Action<TestItem> action)
        {
            var options = builder.Action(action).Route(route);
            pipe = CreatePipe(options);
        }

        void SetupNext(Action<TestItem>? action = null, Func<TestItem, bool>? filter = null, Func<TestItem, IPipe<TestItem>>? route = null, IPipe<TestItem>? afterNext = null)
        {
            SetupNextAsync(x => {
                Debug.Assert(action != null, nameof(action) + " != null");
                action(x);
                return Task.CompletedTask;
            }, filter, route, afterNext);
        }

        void SetupNextAsync(Func<TestItem, Task>? action = null, Func<TestItem, bool>? filter = null, Func<TestItem, IPipe<TestItem>>? route = null, IPipe<TestItem>? afterNext = null)
        {
            var options = builder.Action(action ?? (_ => Task.CompletedTask));
            if (filter != null) 
                options = options.Filter(filter);

            if (route != null)
                options = options.Route(route);

            next = options.ToPipe();
            pipe.LinkTo(next);

            if (afterNext != null)
                next.LinkTo(afterNext);
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

        void AssertWorkingCount(int expected) => Assert.That(pipe.WorkingCount, Is.EqualTo(expected));

        class TestItem
        {
            public string? Data;
        }
    }
}