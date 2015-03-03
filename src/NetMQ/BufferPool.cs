using System;
using System.ServiceModel.Channels;
using System.Threading;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// The IBufferPool interface specifies two methods: Take, and Return.
    /// These provide for taking byte-array data from a common pool, and returning it.
    /// </summary>
    public interface IBufferPool : IDisposable
    {
        /// <summary>
        /// Take byte-array storage from the buffer-pool.
        /// </summary>
        /// <param name="size">the number of bytes to take</param>
        /// <returns>a byte-array that comes from the buffer-pool</returns>
        [NotNull]
        byte[] Take(int size);

        /// <summary>
        /// Return the given byte-array buffer to the common buffer-pool.
        /// </summary>
        /// <param name="buffer">the byte-array to return to the buffer-pool</param>
        void Return([NotNull] byte[] buffer);
    }

    /// <summary>
    /// This implementation of <see cref="IBufferPool"/> uses WCF's <see cref="BufferManager"/>
    /// class to manage a pool of buffers.
    /// </summary>
    public class BufferManagerBufferPool : IBufferPool
    {
        private readonly BufferManager m_bufferManager;

        public BufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            m_bufferManager = BufferManager.CreateBufferManager(maxBufferPoolSize, maxBufferSize);
        }

        public byte[] Take(int size)
        {
            return m_bufferManager.TakeBuffer(size);
        }

        public void Return(byte[] buffer)
        {
            m_bufferManager.ReturnBuffer(buffer);
        }

        public void Dispose()
        {
            m_bufferManager.Clear();
        }
    }

    /// <summary>
    /// This simple implementation of <see cref="IBufferPool"/> does no buffer pooling. Instead, it uses regular
    /// .NET memory management to allocate a buffer each time <see cref="Take"/> is called. Unused buffers
    /// passed to <see cref="Return"/> are simply left for the .NET garbage collector to deal with.
    /// </summary>
    public class GCBufferPool : IBufferPool
    {
        public byte[] Take(int size)
        {
            return new byte[size];
        }

        public void Return(byte[] buffer)
        {}

        public void Dispose()
        {}
    }

    /// <summary>
    /// Contains a singleton instance of <see cref="IBufferPool"/> used for allocating byte arrays
    /// for <see cref="Msg"/> instances with type <see cref="MsgType.Pool"/>.
    /// </summary>
    /// <remarks>
    /// Sending and receiving message frames requires the use of buffers (byte arrays), which are expensive to create and destroy.
    /// You can use the BufferPool class to pool buffers for reuse, reducing allocation, deallocation and garbage collection.
    /// <para/>
    /// The default implementation is <see cref="GCBufferPool"/>.
    /// <list type="bullet">
    /// <item>Call <see cref="SetBufferManagerBufferPool"/> to replace it with a <see cref="BufferManagerBufferPool"/>.</item>
    /// <item>Call <see cref="SetGCBufferPool"/> to reinstate the default <see cref="GCBufferPool"/>.</item>
    /// <item>Call <see cref="SetCustomBufferPool"/> to substitute a custom implementation for the allocation and
    /// deallocation of message buffers.</item>
    /// </list>
    /// </remarks>
    public static class BufferPool
    {
        private static IBufferPool s_bufferPool = new GCBufferPool();

        public static void SetGCBufferPool()
        {
            SetCustomBufferPool(new GCBufferPool());
        }

        public static void SetBufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            SetCustomBufferPool(new BufferManagerBufferPool(maxBufferPoolSize, maxBufferSize));
        }

        public static void SetCustomBufferPool([NotNull] IBufferPool bufferPool)
        {
            var prior = Interlocked.Exchange(ref s_bufferPool, bufferPool);

            if (prior != null)
                prior.Dispose();
        }

        /// <summary>
        /// Allocate a buffer of at least <paramref name="size"/> bytes from the current <see cref="IBufferPool"/>.
        /// </summary>
        /// <param name="size">The minimum size required, in bytes.</param>
        /// <returns>A byte array having at least <paramref name="size"/> bytes.</returns>
        [NotNull]
        public static byte[] Take(int size)
        {
            return s_bufferPool.Take(size);
        }

        /// <summary>
        /// Returns <paramref name="buffer"/> to the <see cref="IBufferPool"/>.
        /// </summary>
        /// <param name="buffer">The byte array to be returned to the pool.</param>
        public static void Return([NotNull] byte[] buffer)
        {
            s_bufferPool.Return(buffer);
        }
    }
}
