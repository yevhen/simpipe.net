namespace Simpipe.Blocks;

using static SharpAssert.Sharp;

[TestFixture]
public class BlockItemFixture
{
    [Test]
    public void Gets_first_value()
    {
        Assert(new BlockItem<int>(42).First() == 42);
        Assert(new BlockItem<int>(new[] {42}).First() == 42);
        Assert(Throws<InvalidOperationException>(() => new BlockItem<int>().First()));
    }

    [Test]
    public void Gets_contained_value()
    {
        Assert(new BlockItem<int>(42).GetValue() == 42);
        Assert(new BlockItem<int>(new[] {42}).GetArray().SequenceEqual(new[] {42}));
    }

    [Test]
    public void Checks_casting()
    {
        Assert(Throws<InvalidCastException>(() => new BlockItem<int>(42).GetArray()));
        Assert(Throws<InvalidCastException>(() => new BlockItem<int>(new[] {42}).GetValue()));
    }

    [Test]
    public void Checks_contained_value()
    {
        Assert(new BlockItem<int>(42).IsValue);
        Assert(!new BlockItem<int>(42).IsArray);
        Assert(!new BlockItem<int>(42).IsEmpty);

        Assert(new BlockItem<int>(new[] {42}).IsArray);
        Assert(!new BlockItem<int>(new[] {42}).IsValue);
        Assert(!new BlockItem<int>(42).IsEmpty);

        Assert(!new BlockItem<int>().IsArray);
        Assert(!new BlockItem<int>().IsValue);
        Assert(new BlockItem<int>().IsEmpty);
    }

    [Test]
    public async Task Applies_with_empty_value()
    {
        var item = BlockItem<int>.Empty;

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert(received.ToArray().SequenceEqual(Array.Empty<int>()));

        received = [];
        item.Apply(x => received.Add(x));

        Assert(received.ToArray().SequenceEqual(Array.Empty<int>()));
    }

    [Test]
    public async Task Applies_with_single_value()
    {
        const int value = 42;
        var item = new BlockItem<int>(value);

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert(received.ToArray().SequenceEqual(new[]{value}));

        received = [];
        item.Apply(x => received.Add(x));

        Assert(received.ToArray().SequenceEqual(new[]{value}));
    }

    [Test]
    public async Task Applies_with_value_array()
    {
        var values = new[] {42, 100};
        var item = new BlockItem<int>(values);

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert(received.ToArray().SequenceEqual(values));

        received = [];
        item.Apply(x => received.Add(x));

        Assert(received.ToArray().SequenceEqual(values));
    }

    [Test]
    public void Filters_with_empty_value()
    {
        Assert(BlockItem<int>.Empty.Where(_ => false) == BlockItem<int>.Empty);
        Assert(BlockItem<int>.Empty.Where(_ => true) == BlockItem<int>.Empty);
    }

    [Test]
    public void Filters_with_single_value()
    {
        var item = new BlockItem<int>(42);

        Assert(item.Where(_ => false) == BlockItem<int>.Empty);

        Assert(item.Where(_ => true) == item);
    }

    [Test]
    public void Filters_with_array()
    {
        var item = new BlockItem<int>(new[] {42, 100});

        Assert(item.Where(_ => false) == BlockItem<int>.Empty);

        Assert(item.Where(_ => true) == item);
        Assert(item.Where(x => x == 42 || x == 100) == item);

        Assert(item.Where(x => x == 100).GetArray().SequenceEqual(new[] {100}));
        Assert(item.Where(x => x == 42).GetArray().SequenceEqual(new[] {42}));
    }

    [Test]
    public void Is_equal_for_empty_value()
    {
        var empty1 = BlockItem<int>.Empty;
        var empty2 = BlockItem<int>.Empty;
        Assert(empty1 == empty2);
    }

    [Test]
    public void Is_equal_for_single_value()
    {
        var item1 = new BlockItem<int>(42);
        var item2 = new BlockItem<int>(42);
        Assert(item1 == item2);
        Assert(new BlockItem<int>(42) != new BlockItem<int>(100));
    }

    [Test]
    public void Is_equal_for_array()
    {
        var values = new[] {42};
        var item1 = new BlockItem<int>(values);
        var item2 = new BlockItem<int>(values);
        Assert(item1 == item2);
        Assert(new BlockItem<int>(values) != new BlockItem<int>(new[] {42}));
    }

    [Test]
    public void Has_correct_size()
    {
        Assert(BlockItem<int>.Empty.Size == 0);
        Assert(new BlockItem<int>(42).Size == 1);
        Assert(new BlockItem<int>(new[] {42, 100}).Size == 2);
    }
}
