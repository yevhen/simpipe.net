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

        static PipeBuilder<int> Builder => new();
    }
}