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
        var ex = Assert.Throws<Exception>(() => pipeline.Add(pipe2));

        Assert.That(ex!.Message, Is.EqualTo("The pipe with id 1 already exists"));
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

        Assert.That(firstProcessed.Single(), Is.EqualTo(42));
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

        Assert.That(firstProcessed.Single(), Is.EqualTo(42));
        Assert.That(secondProcessed, Is.Empty);
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

        Assert.That(firstProcessed, Is.Empty);
        Assert.That(secondProcessed.Single(), Is.EqualTo(42));
    }

    [Test]
    public void Throws_when_starting_from_invalid_pipe_id()
    {
        var firstProcessed = new List<int>();
        var secondProcessed = new List<int>();

        var first = PipeMock<int>.Create(id: "foo", firstProcessed.Add);
        var second = PipeMock<int>.Create(id: "bar", secondProcessed.Add);

        pipeline.Add(first);
        pipeline.Add(second);

        var ex = Assert.ThrowsAsync<PipeNotFoundException>(
            async () => await pipeline.Send(42, "boom"));

        Assert.That(ex!.Message, Is.EqualTo("The pipe with id 'boom' does not exist"));

        Assert.That(firstProcessed, Is.Empty);
        Assert.That(secondProcessed, Is.Empty);
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
        Assert.That(firstProcessed.Single(), Is.EqualTo(1));
        Assert.That(failProcessed.Single(), Is.EqualTo(1));

        // Test non-matching route: item 2 should not route anywhere (stays in originating pipe only)
        await pipeline.Send(2, "2");
        Assert.That(secondProcessed.Single(), Is.EqualTo(2));
        Assert.That(failProcessed.Count, Is.EqualTo(1)); // No new items
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

        Assert.That(pipeline.ToArray(), Is.EqualTo(new[] { first, second, third }));
    }

    [Test]
    public async Task Waits_for_all_pipes_to_complete()
    {
        var first = PipeMock<int>.Create(id: "1");
        var second = PipeMock<int>.Create(id: "2");

        pipeline.Add(first);
        pipeline.Add(second);

        var completion = pipeline.Complete();
        Assert.That(completion.IsCompleted, Is.False);
        Assert.That(pipeline.Completion.IsCompleted, Is.False);

        first.AsBlockMock().SetComplete();
        await Task.Delay(10);
        Assert.That(completion.IsCompleted, Is.False);
        Assert.That(pipeline.Completion.IsCompleted, Is.False);

        second.AsBlockMock().SetComplete();
        await Task.Delay(10);
        await completion;

        Assert.That(completion.IsCompleted, Is.True);
        Assert.That(pipeline.Completion.IsCompleted, Is.True);
    }

    [Test]
    public void Throws_on_SendNext_when_source_id_does_not_exist()
    {
        var pipe = PipeMock<int>.Create(id: "1");
        pipeline.Add(pipe);

        Assert.ThrowsAsync<Exception>(() => pipeline.SendNext(42, "2"));
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

        Assert.That(nextProcessed.Single(), Is.EqualTo(42));
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

        Assert.That(positiveTweet.Sentiment, Is.EqualTo(1));
        Assert.That(positiveTweet.Indexed, Is.True);

        Assert.That(negativeTweet.Sentiment, Is.EqualTo(-1));
        Assert.That(negativeTweet.Indexed, Is.True);
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