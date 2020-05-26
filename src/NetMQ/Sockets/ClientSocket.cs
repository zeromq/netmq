namespace NetMQ.Sockets
{
    /// <summary>
    /// Client socket, thread-safe alternative for Dealer socket
    /// </summary>
    public class ClientSocket : ThreadSafeSocket, IThreadSafeOutSocket, IThreadSafeInSocket
    {
        /// <summary>
        /// Create a new Client Socket.
        /// </summary>
        public ClientSocket() : base(ZmqSocketType.Client)
        {
        }
    }
}