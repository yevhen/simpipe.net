using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using NUnit.Framework;

namespace Youscan.Core.Pipes
{
    [TestFixture]
    public class ActionPipeFixture
    {
        ActionPipe<int> block = null!;
        
        [Test]
        public async Task Executes_action()
        {
            var executed = false;
            Setup(async _ =>
            {
                await Task.Delay(1);
                executed = true;
            });

            await Complete(42);

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
            
            Assert.ThrowsAsync<ArgumentException>(() => Complete(42));
        }

        [Test]
        public async Task Completion_pushes_to_next()
        {
            Setup(async _ =>
            {
                await Task.Delay(100);
            });

            var next = new PipeMock("next");
            block.LinkTo(next);
            
            await Complete(42);
            SpinWait.SpinUntil(() => next.Received.Count > 0, TimeSpan.FromSeconds(2));

            Assert.That(next.Received.Contains(42));
        }

        [Test]
        public async Task Available_capacity_respects_output_count()
        {
            int entered = 0;
            var enteredNext = new ManualResetEventSlim(false);

            Setup(_ =>
            {
                entered++;
            }, boundedCapacity: 3);

            var next = Builder.Action(_ =>
            {
                enteredNext.Set();
                return Task.Delay(TimeSpan.FromDays(1));
            })
            .BoundedCapacity(1)
            .ToPipe();

            block.LinkTo(next);
            
            await Send(1); // blocked in next
            await Send(2);
            await Send(3);

            enteredNext.Wait();
            SpinWait.SpinUntil(() => entered == 3);

            Assert.AreEqual(0, block.InputCount);
            Assert.AreEqual(0, block.WorkingCount);
            Assert.AreEqual(2, block.OutputCount);
            Assert.AreEqual(1, block.AvailableCapacity);
        }

        async Task Complete(params int[] items)
        {
            await Send(items);
            await Complete();
        }

        async Task Send(params int[] items)
        {
            foreach (var item in items)
                await block.Send(item);
        }

        async Task Complete()
        {
            block.Complete();
            await block.Completion;
        }

        void Setup(Func<int, Task> action) => 
            block = Builder.Action(action).ToPipe();

        void Setup(Action<int> action, int boundedCapacity = 1) => 
            block = Builder.Action(action).BoundedCapacity(boundedCapacity).ToPipe();

        static PipeBuilder<int> Builder => new();
    }
}