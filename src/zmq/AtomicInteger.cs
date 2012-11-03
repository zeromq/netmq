using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NetMQ
{
    class AtomicInteger
    {
        int m_value;

        public AtomicInteger(int value)
        {
            m_value = value;
        }

        public int incrementAndGet()
        {
            return Interlocked.Increment(ref m_value);
        }

        public int get()
        {
            Thread.MemoryBarrier();
            return  m_value;
        }

        public void set(int value)
        {
            Interlocked.Exchange(ref m_value, value);
        }

        public int addAndGet(int amount)
        {
            return Interlocked.Add(ref m_value, amount); 
        }

        internal bool compareAndSet(int expect, int update)
        {
            return Interlocked.CompareExchange(ref m_value, update, expect) == expect;
        }
    }
}
