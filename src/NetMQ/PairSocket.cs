using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	/// <summary>
	/// Pair socket, usually used to synchronize two threads, only one socket on each side
	/// </summary>
	public class PairSocket : DuplexSocket<PairSocket>
	{
		public PairSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected override PairSocket This
		{
			get { return this; }
		}
	}
}
