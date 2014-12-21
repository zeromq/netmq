using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Request socket
    /// </summary>
    public class RequestSocket : NetMQSocket, IRequestSocket
    {
        public RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
