using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	public class XPublisherSocket : NetMQSocket
	{
		public XPublisherSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
