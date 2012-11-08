using System.Threading;

namespace NetMQ.zmq
{
    class AtomicLong
    {
        long m_value;

        public AtomicLong(long value)
        {
            m_value = value;
        }

        public long IncrementAndGet()
        {
            return Interlocked.Increment(ref m_value);
        }

        public long Get()
        {
            return Interlocked.Read(ref m_value);
        }

        public void Set(long value)
        {
            Interlocked.Exchange(ref m_value, value);
        }

        public long AddAndGet(long amount)
        {
            return Interlocked.Add(ref m_value, amount); 
        }

        internal bool CompareAndSet(long expect, long update)
        {
            return Interlocked.CompareExchange(ref m_value, update, expect) == expect;
        }
    }
}
