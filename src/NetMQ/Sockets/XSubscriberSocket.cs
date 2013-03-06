using NetMQ.zmq;

namespace NetMQ.Sockets
{
	class XSubscriberSocket : NetMQSocket
	{
		public XSubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
