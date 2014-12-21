using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Router socket, the first message is always the identity of the sender
    /// </summary>
	public class RouterSocket : NetMQSocket, IRouterSocket
    {
        public RouterSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
