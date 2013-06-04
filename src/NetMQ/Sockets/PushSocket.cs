using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Part of the push pull pattern, will push messages to push sockets
	/// </summary>
	class PushSocket : NetMQSocket
	{
		public PushSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected internal override Msg ReceiveInternal(SendReceiveOptions options, out bool hasMore)
		{
			throw new NotSupportedException("Push socket doesn't support receiving");
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
