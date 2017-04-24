using System;
using Xunit;

namespace NetMQ.Tests
{
    public class EventDelegatorTests
    {
        private class Args<T> : EventArgs
        {
            public Args(T value) => Value = value;
            public T Value { get; }
        }

        private event EventHandler<Args<int>> Source;

        [Fact]
        public void Basics()
        {
            EventHandler<Args<int>> sourceHandler = null;

            var delegator = new EventDelegator<Args<double>>(
                () => Source += sourceHandler,
                () => Source -= sourceHandler);

            sourceHandler = (sender, args) => delegator.Fire(this, new Args<double>(args.Value / 2.0));

            Assert.Null(Source);

            var value = 0.0;
            var callCount = 0;

            void DelegatorHandler(object sender, Args<double> args)
            {
                value = args.Value;
                callCount++;
            }

            delegator.Event += DelegatorHandler;

            Assert.NotNull(Source);

            Assert.Equal(0, callCount);

            Source(this, new Args<int>(5));
            Assert.Equal(2.5, value);
            Assert.Equal(1, callCount);

            Source(this, new Args<int>(12));
            Assert.Equal(6.0, value);
            Assert.Equal(2, callCount);

            delegator.Event -= DelegatorHandler;

            Assert.Null(Source);
        }
    }
}