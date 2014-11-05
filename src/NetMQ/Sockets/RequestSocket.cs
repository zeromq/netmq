using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Request socket
    /// </summary>
    public class RequestSocket : NetMQSocket
    {
        internal RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
