using NetMQ.Core;

namespace NetMQ.Sockets
{
    public class StreamSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new StreamSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal StreamSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
