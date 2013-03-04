using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Router socket, the first message is always the identity of the sender
	/// </summary>
	class RouterSocket : NetMQSocket, IRouterSocket
	{
		public RouterSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
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
