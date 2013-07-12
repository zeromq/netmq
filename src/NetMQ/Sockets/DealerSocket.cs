using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Dealer socket, the dealer send messages in load balancing and receive in fair queueing.
	/// </summary>
	public class DealerSocket : NetMQSocket
	{
		public DealerSocket(SocketBase socketHandle) : base(socketHandle)
		{
		}
	}
}
