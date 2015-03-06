using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Router socket, the first message is always the identity of the sender
    /// </summary>
    public class RouterSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new RouterSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal RouterSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
