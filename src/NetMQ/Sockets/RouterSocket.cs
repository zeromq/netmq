using System;
using NetMQ.Core;


namespace NetMQ.Sockets
{
    /// <summary>
    /// Router socket, the first message is always the identity of the sender
    /// </summary>
    public class RouterSocket : NetMQSocket
    {
        internal RouterSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
