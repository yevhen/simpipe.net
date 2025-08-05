namespace Simpipe.Utility;

internal record Selector(Func<Task<bool>> Waiter, Func<Task> Execute);

internal static class Select
{
    public class SelectorBuilder
    {
        readonly List<Selector> selectors = [];

        public SelectorBuilder When(Func<Task<bool>> waiter, Func<Task> execute)
        {
            selectors.Add(new Selector(waiter, execute));
            return this;
        }

        public async Task RunUntil(Func<bool> condition)
        {
            if (selectors.Count == 0)
                throw new InvalidOperationException("No selectors defined.");

            var tasks = selectors.Select(s => s.Waiter()).ToArray();

            while (condition())
            {
                var completedTask = await Task.WhenAny(tasks);
                if (!await completedTask)
                    break;

                var index = Array.IndexOf(tasks, completedTask);
                var selector = selectors[index];

                await selector.Execute();
                tasks[index] = selector.Waiter();
            }
        }
    }

    public static SelectorBuilder When(Func<Task<bool>> waiter, Func<Task> execute) =>
        new SelectorBuilder().When(waiter, execute);
}