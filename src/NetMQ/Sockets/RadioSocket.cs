namespace NetMQ.Sockets
{
    /// <summary>
    /// Radio socket, thread-safe alternative for PUB socket
    /// </summary>
    public class RadioSocket : ThreadSafeSocket, IGroupOutSocket
    {
        /// <summary>
        /// Create a new Client Socket.
        /// </summary>
        public RadioSocket() : base(ZmqSocketType.Radio)
        {
        }
    }
}