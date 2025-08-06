using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes;

[TestFixture]
public class BlockItemFixture
{
    [Test]
    public void Get_first_value()
    {
        Assert.That(new BlockItem<int>(42).First(), Is.EqualTo(42));
        Assert.That(new BlockItem<int>([42]).First(), Is.EqualTo(42));
        Assert.Throws<InvalidOperationException>(() => new BlockItem<int>().First());
    }

    [Test]
    public void Get_contained_value()
    {
        Assert.That(new BlockItem<int>(42).GetValue(), Is.EqualTo(42));
        Assert.That(new BlockItem<int>([42]).GetArray(), Is.EqualTo(new[]{42}));
    }

    [Test]
    public void Checks_casting()
    {
        Assert.Throws<InvalidCastException>(() => new BlockItem<int>(42).GetArray());
        Assert.Throws<InvalidCastException>(() => new BlockItem<int>([42]).GetValue());
    }

    [Test]
    public void Check_contained_value()
    {
        Assert.True(new BlockItem<int>(42).IsValue);
        Assert.False(new BlockItem<int>(42).IsArray);
        Assert.False(new BlockItem<int>(42).IsEmpty);

        Assert.True(new BlockItem<int>([42]).IsArray);
        Assert.False(new BlockItem<int>([42]).IsValue);
        Assert.False(new BlockItem<int>(42).IsEmpty);

        Assert.False(new BlockItem<int>().IsArray);
        Assert.False(new BlockItem<int>().IsValue);
        Assert.True(new BlockItem<int>().IsEmpty);
    }

    [Test]
    public async Task Apply_with_empty_value()
    {
        var item = BlockItem<int>.Empty;

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert.That(received.ToArray(), Is.EqualTo(Array.Empty<int>()));

        received = [];
        item.Apply(x => received.Add(x));

        Assert.That(received.ToArray(), Is.EqualTo(Array.Empty<int>()));
    }

    [Test]
    public async Task Apply_with_single_value()
    {
        const int value = 42;
        var item = new BlockItem<int>(value);

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert.That(received.ToArray(), Is.EqualTo(new[]{value}));

        received = [];
        item.Apply(x => received.Add(x));

        Assert.That(received.ToArray(), Is.EqualTo(new[]{value}));
    }

    [Test]
    public async Task Apply_with_value_array()
    {
        var values = new[]{42, 100};
        var item = new BlockItem<int>(values);

        var received = new List<int>();
        await item.Apply(x =>
        {
            received.Add(x);
            return Task.CompletedTask;
        });

        Assert.That(received.ToArray(), Is.EqualTo(values));

        received = [];
        item.Apply(x => received.Add(x));

        Assert.That(received.ToArray(), Is.EqualTo(values));
    }

    [Test]
    public void Filter_with_empty_value()
    {
        Assert.That(BlockItem<int>.Empty.Where(_ => false), Is.EqualTo(BlockItem<int>.Empty));
        Assert.That(BlockItem<int>.Empty.Where(_ => true), Is.EqualTo(BlockItem<int>.Empty));
    }

    [Test]
    public void Filter_with_single_value()
    {
        var item = new BlockItem<int>(42);

        Assert.That(item.Where(_ => false), Is.EqualTo(BlockItem<int>.Empty));

        Assert.That(item.Where(_ => true), Is.EqualTo(item));
    }

    [Test]
    public void Filter_with_array()
    {
        var item = new BlockItem<int>([42, 100]);

        Assert.That(item.Where(_ => false), Is.EqualTo(BlockItem<int>.Empty));

        Assert.That(item.Where(_ => true), Is.EqualTo(item));
        Assert.That(item.Where(x => x is 42 or 100), Is.EqualTo(item));

        Assert.That(item.Where(x => x == 100).GetArray(), Is.EqualTo(new[]{100}));
        Assert.That(item.Where(x => x == 42).GetArray(), Is.EqualTo(new[]{42}));
    }

    [Test]
    public void Equals_for_empty_value()
    {
        var empty1 = BlockItem<int>.Empty;
        var empty2 = BlockItem<int>.Empty;
        Assert.That(empty1, Is.EqualTo(empty2));
    }

    [Test]
    public void Equals_for_single_value()
    {
        var item1 = new BlockItem<int>(42);
        var item2 = new BlockItem<int>(42);
        Assert.That(item1, Is.EqualTo(item2));
        Assert.That(new BlockItem<int>(42), Is.Not.EqualTo(new BlockItem<int>(100)));
    }

    [Test]
    public void Equals_for_array()
    {
        var values = new[]{42};
        var item1 = new BlockItem<int>(values);
        var item2 = new BlockItem<int>(values);
        Assert.That(item1, Is.EqualTo(item2));
        Assert.That(new BlockItem<int>(values), Is.Not.EqualTo(new BlockItem<int>([42])));
    }

    [Test]
    public void Item_size()
    {
        Assert.That(BlockItem<int>.Empty.Size, Is.EqualTo(0));
        Assert.That(new BlockItem<int>(42).Size, Is.EqualTo(1));
        Assert.That(new BlockItem<int>([42, 100]).Size, Is.EqualTo(2));
    }
}