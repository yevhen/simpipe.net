using System;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Youscan.Core.Pipes
{
    [TestFixture]
    public class PipelineFixture
    {
        Pipeline<int> pipeline = null!;

        [SetUp]
        public void SetUp()
        {
            pipeline = CreatePipeline();
        }

        [Test]
        public void Duplicate_id()
        {
            AddPipe("1");
            var ex = Assert.Throws<Exception>(() => AddPipe("1"));
            
            Assert.AreEqual("The pipe with id 1 already exists", ex!.Message);
        }

        [Test]
        public void Linking_order()
        {
            var first = AddPipe("1");
            var second = AddPipe("2");

            AssertNext(first, second);
            AssertNext(second, null);
        }

        [Test]
        public async Task Send_delegates_to_head()
        {
            var first = AddPipe("1");
            AddPipe("2");
            
            await Send();
            
            AssertSendExecuted(first);
        }

        [Test]
        public async Task Send_to_arbitrary_pipe_by_id_head()
        {
            var first = AddPipe("1");
            var second = AddPipe("2");
            
            await Send("1");
            
            AssertSendExecuted(first);
            AssertSendNotExecuted(second);
        }

        [Test]
        public async Task Send_to_arbitrary_pipe_by_id_not_head()
        {
            var first = AddPipe("1");
            var second = AddPipe("2");
            
            await Send("2");
            
            AssertSendNotExecuted(first);
            AssertSendExecuted(second);
        }

        [Test]
        public void Start_processing_from_arbitrary_pipe_invalid_id()
        {
            var first = AddPipe("foo");
            var second = AddPipe("bar");
            
            var ex = Assert.ThrowsAsync<PipeNotFoundException>(
                async () => await Send("boom"));

            Assert.AreEqual("The pipe with id 'boom' does not exist", ex!.Message);

            AssertSendNotExecuted(first);
            AssertSendNotExecuted(second);
        }

        [Test]
        public void Insert_default_route()
        {
            var fail = new PipeMock("-1");
            var first = new PipeMock("1");
            var second = new PipeMock("2");

            Func<int, IPipe<int>?> defaultRoute = x => x == 1 ? fail : null;
            pipeline = new Pipeline<int>(defaultRoute)
            {
                first,
                second
            };

            Assert.That(first.Routes.Count, Is.EqualTo(1));
            Assert.That(first.Routes[0], Is.EqualTo(defaultRoute));
            
            Assert.That(second.Routes.Count, Is.EqualTo(1));
            Assert.That(second.Routes[0], Is.EqualTo(defaultRoute));
        }

        [Test]
        public void Enumerates_pipes_in_order_of_addition()
        {
            var first = AddPipe("2");
            var second = AddPipe("1");
            var third = AddPipe("A");
            
            CollectionAssert.AreEqual(new[] { first, second, third }, pipeline.ToArray());
        }

        [Test]
        public async Task Completes_on_one_pipe()
        {
            var pipe = AddPipe("1");
            
            Assert.False(pipeline.Completion.IsCompleted);

            var completion = pipeline.Complete();
            
            Assert.False(completion.IsCompleted);
            Assert.False(pipeline.Completion.IsCompleted);
            pipe.ResolveCompletion();

            await completion;
            
            Assert.True(pipe.CompleteExecuted);
            Assert.True(pipeline.Completion.IsCompleted);
        }

        [Test]
        public void Completes_on_many_pipes()
        {
            var first = AddPipe("1");
            var second = AddPipe("2");
            
            Assert.False(pipeline.Completion.IsCompleted);

            var completion = pipeline.Complete();
            
            Assert.False(completion.IsCompleted);
            Assert.True(first.CompleteExecuted);
            Assert.False(second.CompleteExecuted);
            
            first.ResolveCompletion();

            Assert.True(second.CompleteExecuted);
            Assert.False(completion.IsCompleted);
            Assert.False(pipeline.Completion.IsCompleted);

            second.ResolveCompletion();
            
            Assert.True(completion.IsCompleted);
            Assert.True(pipeline.Completion.IsCompleted);
        }

        [Test]
        public void SendNext_source_id_not_exists()
        {
            AddPipe("1");

            Assert.ThrowsAsync<Exception>(() => SendNext("2"));
        }

        [Test]
        public async Task SendNext_source_id_exists()
        {
            var pipe = AddPipe("1");

            await SendNext("1");
            
            Assert.True(pipe.SendNextExecuted);
        }
        
        static void AssertNext(PipeMock pipe, PipeMock? next) => Assert.AreEqual(next, pipe.Next);

        static void AssertSendExecuted(PipeMock pipe) => Assert.True(pipe.SendExecuted);

        static void AssertSendNotExecuted(PipeMock pipe) => Assert.False(pipe.SendExecuted);

        async Task Send(string? id = null) => await pipeline.Send(42, id);

        async Task SendNext(string id) => await pipeline.SendNext(42, id);

        PipeMock AddPipe(string id)
        {
            var pipe = new PipeMock(id);
            pipeline.Add(pipe);
            return pipe;
        }

        static Pipeline<int> CreatePipeline() => new();
    }
}