using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NetMQ
{
    class AtomicLong
    {
        long m_value;

        public AtomicLong(long value)
        {
            m_value = value;
        }

        public long incrementAndGet()
        {
            return Interlocked.Increment(ref m_value);
        }

        public long get()
        {
            return Interlocked.Read(ref m_value);
        }

        public void set(long value)
        {
            Interlocked.Exchange(ref m_value, value);
        }

        public long addAndGet(long amount)
        {
            return Interlocked.Add(ref m_value, amount); 
        }

        internal bool compareAndSet(long expect, long update)
        {
            return Interlocked.CompareExchange(ref m_value, update, expect) == expect;
        }
    }
}
