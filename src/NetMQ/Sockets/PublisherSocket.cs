using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Publisher socket, is the pub in pubsub pattern. publish a message to all subscribers which subscribed for the topic
	/// </summary>
	class PublisherSocket : BaseSocket, IPublisherSocket
	{		
		public PublisherSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected internal override Msg ReceiveInternal(SendRecieveOptions options, out bool hasMore)
		{
			throw new NotSupportedException("Publisher doesn't support receiving");
		}
	}
}
