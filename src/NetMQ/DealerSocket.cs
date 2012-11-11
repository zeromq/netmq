using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	/// <summary>
	/// Dealer socket, the dealer send messages in load balancing and receive in fair queueing.
	/// </summary>
	public class DealerSocket : DuplexSocket<DealerSocket>
	{
		public DealerSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected override DealerSocket This
		{
			get { return this; }
		}
	}
}
