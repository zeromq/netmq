using NetMQ.Core.Utils;
using System;
using System.Threading;

namespace NetMQ.Tests
{
    public sealed class MockCounterPool : IAtomicCounterPool
    {
        private int m_takeCallCount;
        private int m_returnCallCount;

        public int TakeCallCount => Volatile.Read(ref m_takeCallCount);
        public int ReturnCallCount => Volatile.Read(ref m_returnCallCount);
        
        public MockCounterPool()
        {
            
        }

        public void Reset()
        {
            Volatile.Write(ref m_takeCallCount, 0);
            Volatile.Write(ref m_returnCallCount, 0);
        }

        public AtomicCounter Take()
        {
            Interlocked.Increment(ref m_takeCallCount);
            return new AtomicCounter();
        }

        public void Return(AtomicCounter counter)
        {
            Interlocked.Increment(ref m_returnCallCount);
        }

        void IDisposable.Dispose()
        {
        }
    }
}