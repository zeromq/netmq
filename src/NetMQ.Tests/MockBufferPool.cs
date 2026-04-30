using System;
using System.Collections.Generic;

namespace NetMQ.Tests
{

    public sealed class MockBufferPool : IBufferPool
    {
        private readonly object m_lock = new object();

        private int m_takeCallCount;
        private int m_returnCallCount;

        public int TakeCallCount { get { lock (m_lock) return m_takeCallCount; } }
        public List<int> TakeSize { get; }

        public int ReturnCallCount { get { lock (m_lock) return m_returnCallCount; } }
        public List<byte[]> ReturnBuffer { get; }

        public MockBufferPool()
        {
            TakeSize = new List<int>();
            ReturnBuffer = new List<byte[]>();
        }

        public void Reset()
        {
            lock (m_lock)
            {
                m_takeCallCount = 0;
                TakeSize.Clear();

                m_returnCallCount = 0;
                ReturnBuffer.Clear();
            }
        }

        public byte[] Take(int size)
        {
            lock (m_lock)
            {
                m_takeCallCount++;
                TakeSize.Add(size);
            }

            return new byte[size];
        }

        public void Return(byte[] buffer)
        {
            lock (m_lock)
            {
                m_returnCallCount++;
                ReturnBuffer.Add(buffer);
            }
        }

        void IDisposable.Dispose()
        {
        }
    }
}