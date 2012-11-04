using System.Threading;

namespace zmq
{
    class AtomicInteger
    {
        int m_value;

        public AtomicInteger(int value)
        {
            m_value = value;
        }

        public int IncrementAndGet()
        {
            return Interlocked.Increment(ref m_value);
        }

        public int Get()
        {
            Thread.MemoryBarrier();
            return  m_value;
        }

        public void Set(int value)
        {
            Interlocked.Exchange(ref m_value, value);
        }

        public int AddAndGet(int amount)
        {
            return Interlocked.Add(ref m_value, amount); 
        }

        internal bool CompareAndSet(int expect, int update)
        {
            return Interlocked.CompareExchange(ref m_value, update, expect) == expect;
        }
    }
}
