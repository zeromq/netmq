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

        /// <summary>
        /// Create a new BufferManagerBufferPool with the specified maximum buffer pool size
        /// and a maximum size for each individual buffer in the pool.
        /// </summary>
        /// <param name="maxBufferPoolSize">the maximum size to allow for the buffer pool</param>
        /// <param name="maxBufferSize">the maximum size to allow for each individual buffer in the pool</param>
        /// <exception cref="InsufficientMemoryException">There was insufficient memory to create the requested buffer pool.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Either maxBufferPoolSize or maxBufferSize was less than zero.</exception>
        public BufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            m_bufferManager = BufferManager.CreateBufferManager(maxBufferPoolSize, maxBufferSize);
        }

        /// <summary>
        /// Return a byte-array buffer of at least the specified size from the pool.
        /// </summary>
        /// <param name="size">the size in bytes of the requested buffer</param>
        /// <returns>a byte-array that is the requested size</returns>
        /// <exception cref="ArgumentOutOfRangeException">size cannot be less than zero</exception>
        public byte[] Take(int size)
        {
            return m_bufferManager.TakeBuffer(size);
        }

        /// <summary>
        /// Return the given buffer to this manager pool.
        /// </summary>
        /// <param name="buffer">a reference to the buffer being returned</param>
        /// <exception cref="ArgumentException">the Length of buffer does not match the pool's buffer length property</exception>
        /// <exception cref="ArgumentNullException">the buffer reference cannot be null</exception>
        public void Return(byte[] buffer)
        {
            m_bufferManager.ReturnBuffer(buffer);
        }

        /// <summary>
        /// Release the buffers currently cached in this manager.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release the buffers currently cached in this manager.
        /// </summary>
        /// <param name="disposing">true if managed resources are to be disposed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

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
        /// <summary>
        /// Return a newly-allocated byte-array buffer of at least the specified size from the pool.
        /// </summary>
        /// <param name="size">the size in bytes of the requested buffer</param>
        /// <returns>a byte-array that is the requested size</returns>
        /// <exception cref="OutOfMemoryException">there is not sufficient memory to allocate the requested memory</exception>
        public byte[] Take(int size)
        {
            return new byte[size];
        }

        /// <summary>
        /// The expectation of an actual buffer-manager is that this method returns the given buffer to the manager pool.
        /// This particular implementation does nothing.
        /// </summary>
        /// <param name="buffer">a reference to the buffer being returned</param>
        public void Return(byte[] buffer)
        { }

        /// <summary>
        /// The expectation of an actual buffer-manager is that the Dispose method will release the buffers currently cached in this manager.
        /// This particular implementation does nothing.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release the buffers currently cached in this manager (however in this case, this does nothing).
        /// </summary>
        /// <param name="disposing">true if managed resources are to be disposed</param>
        protected virtual void Dispose(bool disposing)
        {
        }
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

        /// <summary>
        /// Set BufferPool to use the <see cref="GCBufferPool"/> (which it does by default).
        /// </summary>
        public static void SetGCBufferPool()
        {
            SetCustomBufferPool(new GCBufferPool());
        }

        /// <summary>
        /// Set BufferPool to use the <see cref="BufferManagerBufferPool"/> to manage the buffer-pool.
        /// </summary>
        /// <param name="maxBufferPoolSize">the maximum size to allow for the buffer pool</param>
        /// <param name="maxBufferSize">the maximum size to allow for each individual buffer in the pool</param>
        /// <exception cref="InsufficientMemoryException">There was insufficient memory to create the requested buffer pool.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Either maxBufferPoolSize or maxBufferSize was less than zero.</exception>
        public static void SetBufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            SetCustomBufferPool(new BufferManagerBufferPool(maxBufferPoolSize, maxBufferSize));
        }

        /// <summary>
        /// Set BufferPool to use the specified IBufferPool implementation to manage the buffer-pool.
        /// </summary>
        /// <param name="bufferPool">the implementation of <see cref="IBufferPool"/> to use</param>
        public static void SetCustomBufferPool([NotNull] IBufferPool bufferPool)
        {
            var prior = Interlocked.Exchange(ref s_bufferPool, bufferPool);

            prior?.Dispose();
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
