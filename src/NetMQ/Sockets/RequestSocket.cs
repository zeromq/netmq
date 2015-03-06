using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A RequestSocket is a NetMQSockete intended to be used as the Request part of the Request-Response pattern.
    /// This is generally paired with a ResponseSocket.
    /// </summary>
    public class RequestSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new RequestSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
