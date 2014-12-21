using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Response socket
    /// </summary>
	public class ResponseSocket : NetMQSocket, IResponseSocket
    {
        public ResponseSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
