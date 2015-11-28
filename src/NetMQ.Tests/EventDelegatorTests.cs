using System;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class EventDelegatorTests
    {
        private class Args<T> : EventArgs
        {
            public Args(T value)
            {
                Value = value;
            }

            public T Value { get; }
        }

        private event EventHandler<Args<int>> Source;

        [Test]
        public void Basics()
        {
            EventHandler<Args<int>> sourceHandler = null;

            var delegator = new EventDelegator<Args<double>>(
                () => Source += sourceHandler,
                () => Source -= sourceHandler);

            sourceHandler = (sender, args) => delegator.Fire(this, new Args<double>(args.Value / 2.0));

            Assert.IsNull(Source);

            var value = 0.0;
            var callCount = 0;

            EventHandler<Args<double>> delegatorHandler
                = (sender, args) =>
                {
                    value = args.Value;
                    callCount++;
                };

            delegator.Event += delegatorHandler;

            Assert.IsNotNull(Source);

            Assert.AreEqual(0, callCount);

            Source(this, new Args<int>(5));
            Assert.AreEqual(2.5, value);
            Assert.AreEqual(1, callCount);

            Source(this, new Args<int>(12));
            Assert.AreEqual(6.0, value);
            Assert.AreEqual(2, callCount);

            delegator.Event -= delegatorHandler;

            Assert.IsNull(Source);
        }
    }
}