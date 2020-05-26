namespace NetMQ.Sockets
{
    /// <summary>
    /// Gather socket, thread-safe alternative for Pull socket
    /// </summary>
    public class GatherSocket : ThreadSafeSocket, IThreadSafeInSocket
    {
        /// <summary>
        /// Create a new Gather Socket.
        /// </summary>
        public GatherSocket() : base(ZmqSocketType.Gather)
        {
        }
    }
}