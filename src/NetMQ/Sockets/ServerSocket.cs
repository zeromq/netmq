namespace NetMQ.Sockets
{
    /// <summary>
    /// Server socket, thread-safe alternative to <see cref="RouterSocket" /> socket.
    /// </summary>
    public class ServerSocket : ThreadSafeSocket, IRoutingIdSocket
    {
        /// <summary>
        /// Create a new Server Socket.
        /// </summary>
        public ServerSocket() : base(ZmqSocketType.Server)
        {
        }
    }
}
