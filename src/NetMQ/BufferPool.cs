using System.ServiceModel.Channels;
using System.Threading;
using JetBrains.Annotations;

namespace NetMQ
{
    public interface IBufferPool
    {
        [NotNull]
        byte[] Take(int size);

        void Return([NotNull] byte[] buffer);
    }

    public class BufferManagerBufferPool : IBufferPool
    {
        private readonly BufferManager m_bufferManager;

        public BufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            m_bufferManager = BufferManager.CreateBufferManager(maxBufferPoolSize, maxBufferSize);
        }

        [NotNull]
        public byte[] Take(int size)
        {
            return m_bufferManager.TakeBuffer(size);
        }

        public void Return([NotNull] byte[] buffer)
        {
            m_bufferManager.ReturnBuffer(buffer);
        }
    }

    public class GCBufferPool : IBufferPool
    {
        [NotNull]
        public byte[] Take(int size)
        {
            return new byte[size];
        }

        public void Return([NotNull] byte[] buffer)
        {
        }
    }

    public static class BufferPool
    {
        private static IBufferPool m_bufferPool;               
       
        static BufferPool()
        {            
            m_bufferPool = new GCBufferPool();
        }

        public static void SetBufferManagerBufferPool(long maxBufferPoolSize, int maxBufferSize)
        {
            Interlocked.Exchange(ref m_bufferPool, new BufferManagerBufferPool(maxBufferPoolSize, maxBufferSize));
        }

        public static void SetCustomBufferPool([NotNull] IBufferPool bufferPool)
        {
            Interlocked.Exchange(ref m_bufferPool, bufferPool);
        }

        [NotNull]
        public static byte[] Take(int size)
        {
            return m_bufferPool.Take(size);
        }

        public static void Return([NotNull] byte[] buffer)
        {
            m_bufferPool.Return(buffer);
        }
    }
}
