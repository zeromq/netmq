using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	/// <summary>
	/// Part of the push pull pattern, will pull messages from push socket
	/// </summary>
	public class PullSocket : BaseSocket
	{
		public PullSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		public byte[] Receive(out bool isMore)
		{
			var msg = ReceiveInternal(SendRecieveOptions.None, out isMore);

			return msg.Data;
		}

		public byte[] Receive(bool dontWait, out bool isMore)
		{
			var msg = ReceiveInternal(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out isMore);

			return msg.Data;
		}

		public string ReceiveString(out bool hasMore)
		{
			return ReceiveStringInternal(SendRecieveOptions.None, out hasMore);
		}

		public string ReceiveString(bool dontWait, out bool hasMore)
		{
			return ReceiveStringInternal(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out hasMore);
		}

		public IList<byte[]> ReceiveAll()
		{
			return base.ReceiveAllInternal();
		}

		public IList<string> ReceiveAllString()
		{
			return base.ReceiveAllStringInternal();
		}
	}
}
