using NetMQ.zmq;

namespace NetMQ.Sockets
{
    public class StreamSocket : NetMQSocket
    {
        internal StreamSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
