namespace Simpipe.Tests.Pipes;

[TestFixture]
public class ApiExplorationFixture
{
    record Selector(int Id, TaskCompletionSource Completion)
    {
        public Selector Copy() => this with { Completion = new TaskCompletionSource() };
        public void Complete() => Completion.SetResult();
        public override string ToString() => $"{Id} - {Completion.Task.Status}";
    }

    [Test]
    public async Task Select_when_any()
    {
        var selectors = new LinkedList<Selector>();

        var tries = 0;
        while (++tries < 10)
        {
            selectors.ElementAt(Random.Shared.Next(0, 2)).Complete();

            var tasks = selectors.Select(x => x.Completion.Task).ToArray();
            var completed = await Task.WhenAny(tasks);

            var indexCompleted = Array.IndexOf(tasks, completed);
            var selector = selectors.ElementAt(indexCompleted);
            Console.WriteLine($"{indexCompleted}: {selector.Id}");

            selectors.Remove(selector);
            selectors.AddLast(selector.Copy());
        }
    }
}