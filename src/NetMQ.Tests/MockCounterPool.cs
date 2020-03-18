using NetMQ.Core.Utils;
using System;

namespace NetMQ.Tests
{
    public sealed class MockCounterPool : IAtomicCounterPool
    {
        public int TakeCallCount { get; private set; }        
        public int ReturnCallCount { get; private set; }
        
        public MockCounterPool()
        {
            
        }

        public void Reset()
        {
            TakeCallCount = 0;
            ReturnCallCount = 0;            
        }

        public AtomicCounter Take()
        {
            TakeCallCount++;
            return new AtomicCounter();
        }

        public void Return(AtomicCounter counter)
        {
            ReturnCallCount++;
        }

        void IDisposable.Dispose()
        {
        }
    }
}