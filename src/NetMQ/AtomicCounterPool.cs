using NetMQ.Core.Utils;
using System;
using System.Threading;

namespace NetMQ
{
    /// <summary>
    /// The IBufferPool interface specifies two methods: Take, and Return.
    /// These provide for taking atomic counters from a common pool, and returning them.
    /// </summary>
    public interface IAtomicCounterPool : IDisposable
    {
        /// <summary>
        /// Take an AtomicCounter from the pool.
        /// </summary>
        /// <returns>an atomic counter</returns>
        AtomicCounter Take();

        /// <summary>
        /// Return the given atomic counter to the common pool.
        /// </summary>
        /// <param name="counter">the atomic counter to return to the buffer-pool</param>
        void Return(AtomicCounter counter);
    }

    /// <summary>
    /// This simple implementation of <see cref="IAtomicCounterPool"/> does no pooling. Instead, it uses regular
    /// .NET memory management to allocate a counter each time <see cref="Take"/> is called. Unused counters
    /// passed to <see cref="Return"/> are simply left for the .NET garbage collector to deal with.
    /// </summary>
    public class GCAtomicCounterPool : IAtomicCounterPool
    {
        /// <summary>
        /// Return a newly-allocated atomic counterfrom the pool.
        /// </summary>
        /// <returns>an atomic counter</returns>
        public AtomicCounter Take()
        {
            return new AtomicCounter();
        }

        /// <summary>
        /// The expectation of an actual pool is that this method returns the given counter to the manager pool.
        /// This particular implementation does nothing.
        /// </summary>
        /// <param name="counter">a reference to the counter being returned</param>
        public void Return(AtomicCounter counter)
        { }

        /// <summary>
        /// The expectation of an actual pool is that the Dispose method will release the pools currently cached in this manager.
        /// This particular implementation does nothing.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release the counters currently cached in this manager (however in this case, this does nothing).
        /// </summary>
        /// <param name="disposing">true if managed resources are to be disposed</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    /// Contains a singleton instance of <see cref="IAtomicCounterPool"/> used for allocating atomic counters
    /// for <see cref="Msg"/> instances with type <see cref="MsgType.Pool"/>.
    /// </summary>
    /// <remarks>
    /// Sending and receiving messages with pooling mode, requires the use of atomic counters. 
    /// You can use the AtomicCounterPool class to pool counters for reuse, reducing allocation, deallocation and garbage collection.
    /// <para/>
    /// The default implementation is <see cref="GCAtomicCounterPool"/>.
    /// <list type="bullet">
    /// <item>Call <see cref="SetGCCounterPool"/> to reinstate the default <see cref="GCBufferPool"/>.</item>
    /// <item>Call <see cref="SetCustomCounterPool"/> to substitute a custom implementation for the allocation and
    /// deallocation of atomic counters.</item>
    /// </list>
    /// </remarks>
    public static class AtomicCounterPool
    {
        private static IAtomicCounterPool s_counterPool = new GCAtomicCounterPool();

        /// <summary>
        /// Set BufferPool to use the <see cref="GCBufferPool"/> (which it does by default).
        /// </summary>
        public static void SetGCCounterPool()
        {
            SetCustomCounterPool(new GCAtomicCounterPool());
        }

        /// <summary>
        /// Set BufferPool to use the specified IAtomicCounterPool implementation to manage the pool.
        /// </summary>
        /// <param name="counterPool">the implementation of <see cref="IAtomicCounterPool"/> to use</param>
        public static void SetCustomCounterPool(IAtomicCounterPool counterPool)
        {
            var prior = Interlocked.Exchange(ref s_counterPool, counterPool);

            prior?.Dispose();
        }

        /// <summary>
        /// Allocate an atomic counter from the current <see cref="IAtomicCounterPool"/>.
        /// </summary>
        /// <returns>an atomic counter</returns>
        public static AtomicCounter Take()
        {
            return s_counterPool.Take();
        }

        /// <summary>
        /// Returns <paramref name="counter"/> to the <see cref="IAtomicCounterPool"/>.
        /// </summary>
        /// <param name="counter">The atomic counter to be returned to the pool.</param>
        public static void Return(AtomicCounter counter)
        {
            s_counterPool.Return(counter);
        }
    }
}