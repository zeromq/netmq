using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Publisher socket, is the pub in pubsub pattern. publish a message to all subscribers which subscribed for the topic
	/// </summary>
	public class PublisherSocket : NetMQSocket
	{		
		public PublisherSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected internal override Msg ReceiveInternal(SendReceiveOptions options, out bool hasMore)
		{
			throw new NotSupportedException("Publisher doesn't support receiving");
		}
	}
}
