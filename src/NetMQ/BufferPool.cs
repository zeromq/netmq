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
    /// BufferManagerBufferPool implements IBufferPool to provide a simple buffer-pool.
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
    /// BufferPool contains a IBufferPool and provides a simple common pool of byte-array buffers.
    /// </summary>
    public static class BufferPool
    {
        private static IBufferPool s_bufferPool;
       
        static BufferPool()
        {            
            s_bufferPool = new GCBufferPool();
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

        [NotNull]
        public static byte[] Take(int size)
        {
            return s_bufferPool.Take(size);
        }

        public static void Return([NotNull] byte[] buffer)
        {
            s_bufferPool.Return(buffer);
        }
    }
}
