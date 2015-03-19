using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// This is a NetMQSocket but provides no additional functionality.
    /// You can use it when you need an instance that is a NetMQSocket
    /// but with none of the distinguishing behavior of any of the other socket types.
    /// </summary>
    /// <remarks>
    /// This is provided because NetMQSocket is an abstract class, so you cannot instantiate it directly.
    /// </remarks>
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
