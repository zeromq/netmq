using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Response socket
    /// </summary>
    public class ResponseSocket : NetMQSocket
    {
        internal ResponseSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
