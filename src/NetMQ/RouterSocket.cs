using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	/// <summary>
	/// Router socket, the first message is always the identity of the sender
	/// </summary>
	public class RouterSocket : DuplexSocket<RouterSocket>
	{
		public RouterSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected override RouterSocket This
		{
			get { return this; }
		}
	}
}
