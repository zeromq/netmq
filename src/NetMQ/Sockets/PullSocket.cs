using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Part of the push pull pattern, will pull messages from push socket
	/// </summary>
	public class PullSocket : NetMQSocket
	{
		public PullSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		public override void Send(byte[] data, int length, SendReceiveOptions options)
		{
			throw  new NotSupportedException("Pull socket doesn't support sending");
		}
	}
}
