using NetMQ.zmq;

namespace NetMQ.Sockets
{
	class BaseSubscriberSocket : BaseSocket
	{
		protected BaseSubscriberSocket(SocketBase socketHandle) : base(socketHandle)
		{
		}

		public void Subscribe(string topic)
		{
			base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
		}

		public void Subscribe(byte[] topic)
		{
			base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
		}

		public void Unsubscribe(string topic)
		{
			base.SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
		}

		public void Unsubscribe(byte[] topic)
		{
			base.SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
		}
	}
}