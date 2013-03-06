using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Part of the push pull pattern, will pull messages from push socket
	/// </summary>
	class PullSocket : NetMQSocket
	{
		public PullSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		public override void Send(byte[] data, int length, SendRecieveOptions options)
		{
			throw  new NotSupportedException("Pull socket doesn't support sending");
		}

		public override void Subscribe(byte[] topic)
		{
			throw new NotSupportedException("Subscribe is not supported");
		}

		public override void Subscribe(string topic)
		{
			throw new NotSupportedException("Subscribe is not supported");
		}

		public override void Unsubscribe(byte[] topic)
		{
			throw new NotSupportedException("Unsubscribe is not supported");
		}

		public override void Unsubscribe(string topic)
		{
			throw new NotSupportedException("Unsubscribe is not supported");
		}
	}
}
