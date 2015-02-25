using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A ResponseSocket is a NetMQSockete intended to be used as the Response part of the Request-Response pattern.
    /// This is generally paired with a RequestSocket.
    /// </summary>
    public class ResponseSocket : NetMQSocket
    {
        internal ResponseSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
