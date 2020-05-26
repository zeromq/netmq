namespace NetMQ.Sockets
{
    /// <summary>
    /// Scatter socket, thread-safe alternative for Push socket
    /// </summary>
    public class ScatterSocket : ThreadSafeSocket, IThreadSafeOutSocket
    {
        /// <summary>
        /// Create a new Scatter Socket.
        /// </summary>
        public ScatterSocket() : base(ZmqSocketType.Scatter)
        {
        }
    }
}