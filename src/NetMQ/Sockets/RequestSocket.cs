using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A RequestSocket is a NetMQSockete intended to be used as the Request part of the Request-Response pattern.
    /// This is generally paired with a ResponseSocket.
    /// </summary>
    public class RequestSocket : NetMQSocket
    {
        internal RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
