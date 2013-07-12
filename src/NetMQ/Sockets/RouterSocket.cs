using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Router socket, the first message is always the identity of the sender
	/// </summary>
	public class RouterSocket : NetMQSocket
	{
		public RouterSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
