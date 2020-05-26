namespace NetMQ.Sockets
{
    /// <summary>
    /// Dish socket, thread-safe alternative for SUB socket
    /// </summary>
    public class DishSocket : ThreadSafeSocket, IGroupInSocket
    {
        /// <summary>
        /// Create a new Client Socket.
        /// </summary>
        public DishSocket() : base(ZmqSocketType.Dish)
        {
        }

        /// <summary>
        /// Join the dish socket to a group
        /// </summary>
        /// <param name="group">The group to join</param>
        public void Join(string group)
        {
            m_socketHandle.CheckDisposed();
            m_socketHandle.Join(group);
        }
        
        /// <summary>
        /// Leave a group for a dish socket
        /// </summary>
        /// <param name="group">The group leave</param>
        public void Leave(string group)
        {
            m_socketHandle.CheckDisposed();
            m_socketHandle.Leave(group);
        }
    }
}