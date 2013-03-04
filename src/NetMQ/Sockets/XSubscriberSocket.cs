using NetMQ.zmq;

namespace NetMQ.Sockets
{
	class XSubscriberSocket : NetMQSocket, IXSubscriberSocket
	{
		public XSubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
