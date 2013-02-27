using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Part of the push pull pattern, will push messages to push sockets
	/// </summary>
	class PushSocket : BaseSocket, IPushSocket
	{
		public PushSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		protected internal override Msg ReceiveInternal(SendRecieveOptions options, out bool hasMore)
		{
			throw new NotSupportedException("Push socket doesn't support receiving");
		}
	}
}
