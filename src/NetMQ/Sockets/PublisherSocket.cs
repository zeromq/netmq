using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Publisher socket, is the pub in pubsub pattern. publish a message to all subscribers which subscribed for the topic
	/// </summary>
	class PublisherSocket : NetMQSocket
	{		
		public PublisherSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected internal override Msg ReceiveInternal(SendReceiveOptions options, out bool hasMore)
		{
			throw new NotSupportedException("Publisher doesn't support receiving");
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
