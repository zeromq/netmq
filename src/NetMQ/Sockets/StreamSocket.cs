using NetMQ.zmq;

namespace NetMQ.Sockets
{
    public class StreamSocket : NetMQSocket
    {
        public StreamSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

    }
}
