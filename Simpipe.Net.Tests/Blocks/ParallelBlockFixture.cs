namespace Simpipe.Pipes;

[TestFixture]
public class ParallelBlockFixture
{
    class TestItem
    {
        public int Id { get; set; }
        public string? Value { get; set; }
        public int EnrichedValue { get; set; }
    }

    [Test]
    public async Task Should_execute_single_action()
    {
        var item = new TestItem { Id = 1, Value = "test" };
        var enriched = false;
        
        var parallelBlock = Pipe<TestItem>

        await parallelBlock.Send(item);
        await Complete(parallelBlock);

        Assert.That(enriched, Is.True);
        Assert.That(item.EnrichedValue, Is.EqualTo(2));
    }

    static async Task Complete(Pipe<TestItem> pipe)
    {
        pipe.Complete();
        await pipe.Completion;
    }
}