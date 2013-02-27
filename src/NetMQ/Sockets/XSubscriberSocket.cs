using NetMQ.zmq;

namespace NetMQ.Sockets
{
	class XSubscriberSocket : BaseSubscriberSocket, IXSubscriberSocket
	{
		public XSubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
