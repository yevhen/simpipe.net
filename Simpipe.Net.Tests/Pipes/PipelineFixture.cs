using System.Linq;
using static SharpAssert.Sharp;

namespace Simpipe.Pipes;

[TestFixture]
public class PipelineFixture
{
    Pipeline<int> pipeline = null!;

    [SetUp]
    public void SetUp() => pipeline = new();

    [Test]
    public void Throws_on_duplicate_id()
    {
        var pipe1 = PipeMock<int>.Create(id: "1");
        pipeline.Add(pipe1);

        var pipe2 = PipeMock<int>.Create(id: "1");

        Assert(Throws<Exception>(() => pipeline.Add(pipe2))
            .Message == "The pipe with id 1 already exists");
    }

    [Test]
    public async Task Delegates_send_to_head()
    {
        var firstProcessed = new List<int>();
        var first = PipeMock<int>.Create(id: "1", firstProcessed.Add);
        pipeline.Add(first);

        var second = PipeMock<int>.Create(id: "2");
        pipeline.Add(second);

        await pipeline.Send(42);

        Assert(firstProcessed.Single() == 42);
    }

    [Test]
    public async Task Sends_to_arbitrary_pipe_by_id_when_head()
    {
        var firstProcessed = new List<int>();
        var secondProcessed = new List<int>();

        var first = PipeMock<int>.Create(id: "1", firstProcessed.Add);
        var second = PipeMock<int>.Create(id: "2", secondProcessed.Add);

        pipeline.Add(first);
        pipeline.Add(second);

        first.BreakNextChainToPreventForwarding();

        await pipeline.Send(42, "1");

        Assert(firstProcessed.Single() == 42);
        Assert(!secondProcessed.Any());
    }

    [Test]
    public async Task Sends_to_arbitrary_pipe_by_id_when_not_head()
    {
        var firstProcessed = new List<int>();
        var secondProcessed = new List<int>();

        var first = PipeMock<int>.Create(id: "1", firstProcessed.Add);
        var second = PipeMock<int>.Create(id: "2", secondProcessed.Add);

        pipeline.Add(first);
        pipeline.Add(second);

        first.BreakNextChainToPreventForwarding();

        await pipeline.Send(42, "2");

        Assert(!firstProcessed.Any());
        Assert(secondProcessed.Single() == 42);
    }

    [Test]
    public async Task Throws_when_starting_from_invalid_pipe_id()
    {
        var firstProcessed = new List<int>();
        var secondProcessed = new List<int>();

        var first = PipeMock<int>.Create(id: "foo", firstProcessed.Add);
        var second = PipeMock<int>.Create(id: "bar", secondProcessed.Add);

        pipeline.Add(first);
        pipeline.Add(second);

        var ex = await ThrowsAsync<PipeNotFoundException>(() => pipeline.Send(42, "boom"));
        Assert(ex.Message == "The pipe with id 'boom' does not exist");

        Assert(firstProcessed.Count == 0);
        Assert(secondProcessed.Count == 0);
    }

    [Test]
    public async Task Inserts_default_route()
    {
        var failProcessed = new List<int>();
        var firstProcessed = new List<int>();
        var secondProcessed = new List<int>();

        var fail = PipeMock<int>.Create(id: "-1", failProcessed.Add);
        var first = PipeMock<int>.Create(id: "1", firstProcessed.Add);
        var second = PipeMock<int>.Create(id: "2", secondProcessed.Add);

        Func<int, Pipe<int>?> defaultRoute = x => x == 1 ? fail : null;
        pipeline = new Pipeline<int>(defaultRoute)
        {
            first,
            second
        };

        first.BreakNextChainToPreventForwarding();
        second.BreakNextChainToPreventForwarding();

        // Test the default route behavior: item 1 should route to fail pipe
        await pipeline.Send(1, "1");
        Assert(firstProcessed.Single() == 1);
        Assert(failProcessed.Single() == 1);

        // Test non-matching route: item 2 should not route anywhere (stays in originating pipe only)
        await pipeline.Send(2, "2");
        Assert(secondProcessed.Single() == 2);
        Assert(failProcessed.Count == 1); // No new items
    }

    [Test]
    public void Enumerates_pipes_in_order_of_addition()
    {
        var first = PipeMock<int>.Create(id: "2");
        var second = PipeMock<int>.Create(id: "1");
        var third = PipeMock<int>.Create(id: "A");

        pipeline.Add(first);
        pipeline.Add(second);
        pipeline.Add(third);

        Assert(pipeline.ToArray().SequenceEqual(new[] { first, second, third }));
    }

    [Test]
    public async Task Waits_for_all_pipes_to_complete()
    {
        var first = PipeMock<int>.Create(id: "1");
        var second = PipeMock<int>.Create(id: "2");

        pipeline.Add(first);
        pipeline.Add(second);

        var completion = pipeline.Complete();
        Assert(completion.IsCompleted == false);
        Assert(pipeline.Completion.IsCompleted == false);

        first.AsBlockMock().SetComplete();
        await Task.Delay(10);
        Assert(completion.IsCompleted == false);
        Assert(pipeline.Completion.IsCompleted == false);

        second.AsBlockMock().SetComplete();
        await Task.Delay(10);
        await completion;

        Assert(completion.IsCompleted == true);
        Assert(pipeline.Completion.IsCompleted == true);
    }

    [Test]
    public async Task Throws_on_SendNext_when_source_id_does_not_exist()
    {
        var pipe = PipeMock<int>.Create(id: "1");
        pipeline.Add(pipe);

        Assert(await ThrowsAsync<Exception>(() => pipeline.SendNext(42, "2")));
    }

    [Test]
    public async Task Sends_next_when_source_id_exists()
    {
        var nextProcessed = new List<int>();
        var pipe = PipeMock<int>.Create(id: "1");
        var nextPipe = PipeMock<int>.Create(action: nextProcessed.Add);

        pipe.LinkNext(nextPipe);
        pipeline.Add(pipe);

        await pipeline.SendNext(42, "1");

        Assert(nextProcessed.Single() == 42);
    }

    [Test]
    public async Task Integration_test()
    {
        var sentimentPipe = Pipe<Tweet>
            .Action(tweet => tweet.Sentiment =
                tweet.Text.Contains("Love") ? 1 : tweet.Text.Contains("Hate") ? -1 : 0)
            .Id("sentiment-analyzer");

        var indexPipe = Pipe<Tweet>
            .Batch(100, async tweets => {
                await Task.Delay(100);
                foreach (var tweet in tweets) tweet.Indexed = true;
            })
            .Id("elasticsearch-indexer");

        var pipeline = new Pipeline<Tweet>
        {
            sentimentPipe,
            indexPipe
        };

        var positiveTweet = new Tweet { Text = "Love this product! #awesome" };
        var negativeTweet = new Tweet { Text = "Hate customer service @support" };

        await pipeline.Send(positiveTweet);
        await pipeline.Send(negativeTweet);

        await pipeline.Complete();

        Assert(positiveTweet.Sentiment == 1);
        Assert(positiveTweet.Indexed == true);

        Assert(negativeTweet.Sentiment == -1);
        Assert(negativeTweet.Indexed == true);
    }

    public class Tweet
    {
        public int Sentiment { get; set; }
        public required string Text { get; set; }
        public bool Indexed { get; set; }
    }
}

internal static partial class TestingExtensions
{
    public static void BreakNextChainToPreventForwarding<T>(this Pipe<T> pipe) => pipe.LinkNext(null);
}