using Simpipe.Pipes;

namespace Simpipe.Tests.Pipes
{
    [TestFixture]
    public class PipeItemFixture
    {
        [Test]
        public void Get_first_value()
        {
            Assert.AreEqual(42, new PipeItem<int>(42).First());
            Assert.AreEqual(42, new PipeItem<int>(new[]{42}).First()); 
            Assert.Throws<InvalidOperationException>(() => new PipeItem<int>().First()); 
        }
        
        [Test]
        public void Get_contained_value()
        {
            Assert.AreEqual(42, new PipeItem<int>(42).GetValue());
            Assert.AreEqual(new[]{42}, new PipeItem<int>(new[]{42}).GetArray());
        }
        
        [Test]
        public void Checks_casting()
        {
            Assert.Throws<InvalidCastException>(() => new PipeItem<int>(42).GetArray());
            Assert.Throws<InvalidCastException>(() => new PipeItem<int>(new []{42}).GetValue());
        }
        
        [Test]
        public void Check_contained_value()
        {
            Assert.True(new PipeItem<int>(42).IsValue);
            Assert.False(new PipeItem<int>(42).IsArray);
            Assert.False(new PipeItem<int>(42).IsEmpty);
            
            Assert.True(new PipeItem<int>(new []{42}).IsArray);
            Assert.False(new PipeItem<int>(new []{42}).IsValue);
            Assert.False(new PipeItem<int>(42).IsEmpty);
            
            Assert.False(new PipeItem<int>().IsArray);
            Assert.False(new PipeItem<int>().IsValue);
            Assert.True(new PipeItem<int>().IsEmpty);
        }
        
        [Test]
        public async Task Apply_with_empty_value()
        {
            var item = PipeItem<int>.Empty;
            
            var received = new List<int>();
            await item.Apply(x =>
            {
                received.Add(x);
                return Task.CompletedTask;
            });

            Assert.AreEqual(Array.Empty<int>(), received.ToArray());
            
            received = new List<int>();
            item.Apply(x => received.Add(x));

            Assert.AreEqual(Array.Empty<int>(), received.ToArray());
        }
        
        [Test]
        public async Task Apply_with_single_value()
        {
            const int value = 42;
            var item = new PipeItem<int>(value);
            
            var received = new List<int>();
            await item.Apply(x =>
            {
                received.Add(x);
                return Task.CompletedTask;
            });

            Assert.AreEqual(new[]{value}, received.ToArray());
            
            received = new List<int>();
            item.Apply(x => received.Add(x));

            Assert.AreEqual(new[]{value}, received.ToArray());
        }

        [Test]
        public async Task Apply_with_value_array()
        {
            var values = new[]{42, 100};
            var item = new PipeItem<int>(values);
            
            var received = new List<int>();
            await item.Apply(x =>
            {
                received.Add(x);
                return Task.CompletedTask;
            });

            Assert.AreEqual(values, received.ToArray());
            
            received = new List<int>();
            item.Apply(x => received.Add(x));

            Assert.AreEqual(values, received.ToArray());
        }
        
        [Test]
        public void Filter_with_empty_value()
        {
            Assert.AreEqual(PipeItem<int>.Empty, PipeItem<int>.Empty.Where(x => false));
            Assert.AreEqual(PipeItem<int>.Empty, PipeItem<int>.Empty.Where(x => true));
        }

        [Test]
        public void Filter_with_single_value()
        {
            var item = new PipeItem<int>(42);
            
            Assert.AreEqual(PipeItem<int>.Empty, item.Where(x => false));
            
            Assert.AreEqual(item, item.Where(x => true));
        }

        [Test]
        public void Filter_with_array()
        {
            var item = new PipeItem<int>(new []{42, 100});
            
            Assert.AreEqual(PipeItem<int>.Empty, item.Where(x => false));
            
            Assert.AreEqual(item, item.Where(x => true));
            Assert.AreEqual(item, item.Where(x => x is 42 or 100));
            
            CollectionAssert.AreEqual(new[]{100}, item.Where(x => x == 100).GetArray());
            CollectionAssert.AreEqual(new[]{42}, item.Where(x => x == 42).GetArray());
        }

        [Test]
        public void Equals_for_empty_value()
        {
            Assert.AreEqual(PipeItem<int>.Empty, PipeItem<int>.Empty);
        }
        
        [Test]
        public void Equals_for_single_value()
        {
            Assert.AreEqual(new PipeItem<int>(42), new PipeItem<int>(42));
            Assert.AreNotEqual(new PipeItem<int>(100), new PipeItem<int>(42));
        }
        
        [Test]
        public void Equals_for_array()
        {
            var values = new[]{42};
            Assert.AreEqual(new PipeItem<int>(values), new PipeItem<int>(values));
            Assert.AreNotEqual(new PipeItem<int>(new []{42}), new PipeItem<int>(values));
        }

        [Test]
        public void Item_size()
        {
            Assert.AreEqual(0, PipeItem<int>.Empty.Size);
            Assert.AreEqual(1, new PipeItem<int>(42).Size);
            Assert.AreEqual(2, new PipeItem<int>(new []{42, 100}).Size);
        }
    }
}