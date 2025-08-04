using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using NUnit.Framework;

namespace Youscan.Core.Pipes
{
    [TestFixture]
    class DataFlowApiFixture
    {
        [Test]
        public async Task Most_recent_executor()
        {
            var cts = new CancellationTokenSource();
            var executor = new MostRecentActionExecutor(cts.Token);
            
            var received = new List<int>();
            var producer = Task.Run(() =>
            {
                var i = 0;
                while (!cts.IsCancellationRequested)
                {
                    var item = i++;
                    executor.Execute(async () =>
                    {
                        received.Add(item);
                        await Task.Delay(15, cts.Token);
                    });
                }
            }, cts.Token);

            SpinWait.SpinUntil(() => received.Count == 10);
            cts.Cancel();

            await producer;
            await executor.Completion;

            Console.WriteLine(string.Join(",", received));
        }

        [Test]
        public async Task Linking_behavior()
        {
            var a = new TransformBlock<int, int>(x => x);
            var b = new TransformBlock<int, int>(x => x + 1);
            var c = new TransformBlock<int, int>(x => x - 1);
            var d = new ActionBlock<int>(Console.WriteLine);

            a.LinkTo(new RoutingBlock<int>(x => x % 2 == 0 ? b : c));
            b.LinkTo(d);
            c.LinkTo(d);

            a.Post(11);
            a.Post(20);
            a.Post(31);
            a.Post(42);

            a.Complete();
            await a.Completion;
        }

        [Test]
        public async Task Link_buffer_overrun()
        {
            var results = new List<string>();

            var blocker = new ManualResetEvent(false);
            var entered = 0;

            var a = new TransformBlock<string, string>(x => x);
            var b = new TransformBlock<string, string>(x =>
            {
                entered++;
                blocker.WaitOne();
                return x + "b";
            }, new ExecutionDataflowBlockOptions {BoundedCapacity = 1});
            
            var c = new TransformBlock<string, string>(x =>
            {
                entered++;
                blocker.WaitOne();
                return x + "c";
            }, new ExecutionDataflowBlockOptions {BoundedCapacity = 1});

            var d = new ActionBlock<string>(x => results.Add(x));

            a.LinkTo(b, _ => true);
            a.LinkTo(c, _ => true);

            b.LinkTo(d);
            c.LinkTo(d);

            await a.SendAsync("1");
            await a.SendAsync("2");

            SpinWait.SpinUntil(() => entered == 2);
            blocker.Set();

            a.Complete();
            await a.Completion;

            Assert.That(results, Is.EquivalentTo(new[]{"1b", "2c"}));
        }

        [Test]
        public async Task Batch_block_input_count()
        {
            var b = new BatchBlock<int>(3);
            await b.SendAsync(1);
            await b.SendAsync(2);
            Assert.That(b.OutputCount, Is.EqualTo(0), "Need to write custom logic to get buffered item count");

            await b.SendAsync(2);
            Assert.That(b.OutputCount, Is.EqualTo(1), "Count is measured in batches so we need to multiply to batch size");
        }
        
        [Test]
        public async Task Sending_over_bounded_capacity_block_sender()
        {
            var options = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1
            };

            var pipe = new ActionBlock<int>(_ => Thread.Sleep(200), options);

            await pipe.SendAsync(100500);
            var secondTask = pipe.SendAsync(45);
            await Task.WhenAny(secondTask, Task.Delay(100));
            
            Assert.False(secondTask.IsCompleted);
        }

        [Test]
        public void Posting_over_bounded_capacity_does_not_block_sender()
        {
            var options = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1
            };

            var pipe = new ActionBlock<int>(_ => Thread.Sleep(200), options);

            pipe.Post(100500);
            var completed = pipe.Post(45);
            
            Assert.False(completed);
        }

        [Test]
        public async Task Input_count()
        {
            var received = new ManualResetEvent(false);

            var options = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 2
            };

            var block = new ActionBlock<int>(_ =>
            {
                received.Set();
                Thread.Sleep(TimeSpan.FromDays(1));
            }, options);

            await block.SendAsync(42); // in-flight (blocked)
            await block.SendAsync(42); // buffered

            received.WaitOne();
            
            Assert.AreEqual(1, block.InputCount, "Should not account in-flight items");
        }

        [Test]
        public async Task Null_target()
        {
            var entered = new AutoResetEvent(false);
            var blocker = new AutoResetEvent(false);

            var options = new ExecutionDataflowBlockOptions {BoundedCapacity = 2};
            var a = new TransformBlock<int, int>(x =>
            {
                entered.Set();
                blocker.WaitOne();
                return x;
            }, options);

            var nil = DataflowBlock.NullTarget<int>();
            a.LinkTo(new RoutingBlock<int>(_ => nil));

            await a.SendAsync(1);
            entered.WaitOne();

            Assert.AreEqual(0, a.InputCount);
            Assert.AreEqual(0, a.OutputCount);

            blocker.Set();

            await a.SendAsync(1);
            entered.WaitOne();

            Assert.AreEqual(0, a.InputCount);
            Assert.AreEqual(0, a.OutputCount);

            blocker.Set();
            
            a.Complete();
            await a.Completion;

            Assert.That(a.OutputCount, Is.EqualTo(0));
        }
    }
}