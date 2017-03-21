using System.Threading;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// This class simply provides a counter-value, which may be set, increased, and decremented.
    /// Increase and Decrement are both thread-safe operations.
    /// </summary>
    internal sealed class AtomicCounter
    {
        private int m_value;

        /// <summary>
        /// Create a new AtomicCounter object with an initial counter-value of zero.
        /// </summary>
        public AtomicCounter()
        {
            m_value = 0;
        }

        /// <summary>
        /// Assign the given amount to the counter-value.
        /// </summary>
        /// <param name="amount">the integer value to set the counter to</param>
        public void Set(int amount)
        {
            m_value = amount;
        }

        /// <summary>
        /// Add the given amount to the counter-value, in an atomic thread-safe manner.
        /// </summary>
        /// <param name="amount">the integer amount to add to the counter-value</param>
        public void Increase(int amount)
        {
            Interlocked.Add(ref m_value, amount);
        }

        /// <summary>
        /// Subtract the given amount from the counter-value, in an atomic thread-safe manner.
        /// </summary>
        /// <param name="amount">the integer amount to subtract from to the counter-value (default value is 1)</param>
        /// <returns>the new value of the counter</returns>
        public int Decrement(int amount = 1)
        {
            return Interlocked.Add(ref m_value, amount*-1);
        }
    }
}
