using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A ResponseSocket is a NetMQSocket intended to be used as the Response part of the Request-Response pattern.
    /// This is generally paired with a RequestSocket.
    /// </summary>
    public class ResponseSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new ResponseSocket.
        /// </summary>
        public ResponseSocket() : base(ZmqSocketType.Rep)
        {
            
        }

        /// <summary>
        /// Create a new ResponseSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal ResponseSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
