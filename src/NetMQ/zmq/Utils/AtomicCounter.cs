using System.Threading;

namespace NetMQ.zmq.Utils
{
    class AtomicCounter
    {
        private int m_value;

        public AtomicCounter()
        {
            m_value = 0;
        }

        public void Set(int amount)
        {
            m_value = amount;
        }
        
        public void Increase(int amount)
        {
            Interlocked.Add(ref m_value, amount);
        }

        public int Decrement(int amount = 1)
        {
            return Interlocked.Add(ref m_value, amount*-1);
        }
    }
}
