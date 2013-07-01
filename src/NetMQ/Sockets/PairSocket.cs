using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Pair socket, usually used to synchronize two threads, only one socket on each side
	/// </summary>
	public class PairSocket : NetMQSocket
	{
		public PairSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}
	}
}
