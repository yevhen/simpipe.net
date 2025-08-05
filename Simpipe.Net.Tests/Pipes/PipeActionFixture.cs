using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Youscan.Core.Pipes
{
    [TestFixture]
    public class PipeActionFixture
    {
        int receivedValue;
        int[] receivedArray = null!;
        PipeAction<int> syncSingleValueAction = null!;
        PipeAction<int> asyncSingleValueAction = null!;
        PipeAction<int> syncValueArrayAction = null!;
        PipeAction<int> asyncValueArrayAction = null!;

        [SetUp]
        public void SetUp()
        {
            syncSingleValueAction = PipeAction<int>.For(x => receivedValue = x);
            asyncSingleValueAction = PipeAction<int>.For((int x) =>
            {
                receivedValue = x;
                return Task.CompletedTask;
            });
            syncValueArrayAction = PipeAction<int>.For((int[] x) => receivedArray = x);
            asyncValueArrayAction = PipeAction<int>.For((int[] x) =>
            {
                receivedArray = x;
                return Task.CompletedTask;
            });
        }

        [Test]
        public async Task Executes_single_value()
        {
            const int value = 42;
            
            await syncSingleValueAction.Execute(value);
            Assert.That(receivedValue, Is.EqualTo(value));

            await asyncSingleValueAction.Execute(value);
            Assert.That(receivedValue, Is.EqualTo(value));

            Assert.ThrowsAsync<InvalidCastException>(() => syncValueArrayAction.Execute(value));
            Assert.ThrowsAsync<InvalidCastException>(() => asyncValueArrayAction.Execute(value));
        }

        [Test]
        public async Task Executes_array_value()
        {
            var array = new[]{42, 100};
            
            await syncValueArrayAction.Execute(array);
            Assert.That(receivedArray, Is.EqualTo(array));

            await asyncValueArrayAction.Execute(array);
            Assert.That(receivedArray, Is.EqualTo(array));

            Assert.ThrowsAsync<InvalidCastException>(() => syncSingleValueAction.Execute(array));
            Assert.ThrowsAsync<InvalidCastException>(() => asyncSingleValueAction.Execute(array));
        }
    }
}