using Simpipe.Pipes;

namespace Simpipe.Tests.Blocks;

public class CounterBlockFixture
{
    interface IPipeBlock<T>
    {
        Task Send(T item);
        void SetAction(Func<PipeItem<T>, Task> action);
        void SetDone(Func<T, Task> done);
    }

    class PipeBlockMock<T>(Func<T, Task>? onSend = null) : IPipeBlock<T>
    {
        Func<PipeItem<T>, Task> action = _ => Task.CompletedTask;
        Func<T, Task> done = _ => Task.CompletedTask;

        public void SetAction(Func<PipeItem<T>, Task> action) => this.action = action;
        public void SetDone(Func<T, Task> done) => this.done = done;

        public async Task Send(T item)
        {
            await (onSend != null ? onSend(item) : Task.CompletedTask);
            await action(new PipeItem<T>(item));
            await done(item);
        }
    }

    class CounterBlock<T>(IPipeBlock<T> inner) : IPipeBlock<T>
    {
        volatile int inputCount;
        volatile int outputCount;
        volatile int workingCount;

        public int InputCount => inputCount;
        public int OutputCount => outputCount;
        public int WorkingCount => workingCount;

        public async Task Send(T item)
        {
            Interlocked.Increment(ref inputCount);
            await inner.Send(item);
            Interlocked.Decrement(ref inputCount);
        }

        public void SetAction(Func<PipeItem<T>, Task> action) => inner.SetAction(async x =>
        {
            Interlocked.Increment(ref workingCount);
            await action(x);
            Interlocked.Decrement(ref workingCount);
        });

        public void SetDone(Func<T, Task> done) => inner.SetDone(async x =>
        {
            Interlocked.Increment(ref outputCount);
            await done(x);
            Interlocked.Decrement(ref outputCount);
        });
    }

    [Test]
    public void Input_count_is_zero_on_creation()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);
        Assert.That(counter.InputCount, Is.EqualTo(0));
    }

    [Test]
    public void Input_count_increments_on_send()
    {
        var sendCompletion = new TaskCompletionSource();
        var block = new PipeBlockMock<int>(_ => sendCompletion.Task);
        var counter = new CounterBlock<int>(block);

        _ = counter.Send(42);

        Assert.That(counter.InputCount, Is.EqualTo(1));
    }

    [Test]
    public void Input_count_decrements_on_send_complete()
    {
        var sendCompletion = new TaskCompletionSource();
        var block = new PipeBlockMock<int>(_ => sendCompletion.Task);
        var counter = new CounterBlock<int>(block);

        _ = counter.Send(42);
        sendCompletion.SetResult();

        Assert.That(counter.InputCount, Is.EqualTo(0));
    }

    [Test]
    public void Working_count_is_zero_on_creation()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);
        Assert.That(counter.WorkingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Working_count_increments_on_action_execute()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);

        var workingCount = 0;
        counter.SetAction(_ =>
        {
            workingCount = counter.WorkingCount;
            return Task.CompletedTask;
        });

        await counter.Send(42);

        Assert.That(workingCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Working_count_decrements_on_action_execute()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);

        await counter.Send(42);

        Assert.That(counter.WorkingCount, Is.EqualTo(0));
    }

    [Test]
    public void Output_count_is_zero_on_creation()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);
        Assert.That(counter.InputCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Output_count_increments_on_done()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);

        var outputCount = 0;
        counter.SetDone(_ =>
        {
            outputCount = counter.OutputCount;
            return Task.CompletedTask;
        });

        await counter.Send(42);
        Assert.That(outputCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Output_count_decrements_on_done()
    {
        var block = new PipeBlockMock<int>();
        var counter = new CounterBlock<int>(block);

        await counter.Send(42);

        Assert.That(counter.OutputCount, Is.EqualTo(0));
    }
}