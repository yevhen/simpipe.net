namespace Simpipe.Utility;

internal record Selector(Func<Task> Waiter, Func<Task> Action);

internal static class Select
{
    public static Task Run(Func<bool> runUntil, params Selector[] selectors) => Task.Run(async () =>
    {
        LinkedList<Selector> loop = new(selectors);

        while (runUntil())
        {
            var tasks = loop.Select(x => x.Waiter()).ToArray();
            var completed = await Task.WhenAny(tasks);

            var indexCompleted = Array.IndexOf(tasks, completed);
            var selector = loop.ElementAt(indexCompleted);

            await selector.Action();

            loop.Remove(selector);
            loop.AddLast(selector); // Re-add last to keep fairness
        }
    });
}