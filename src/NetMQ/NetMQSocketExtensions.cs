using System;
using System.Collections.Generic;
using System.Linq;
using NetMQ.zmq;

namespace NetMQ
{
	public static class NetMQSocketExtensions
	{
		public static IEnumerable<byte[]> ReceiveMessages(this NetMQSocket socket)
		{
			bool hasMore = true;

			while (hasMore)
			{
				Msg message = socket.ReceiveInternal(SendReceiveOptions.None, out hasMore);
				yield return message.Data;
			}
		}

		public static IEnumerable<string> ReceiveStringMessages(this NetMQSocket socket)
		{
			bool hasMore = true;

			while (hasMore)
				yield return socket.ReceiveString(SendReceiveOptions.None, out hasMore);
		}

		[Obsolete("Use ReceiveMessages extension method instead")]
		public static IList<byte[]> ReceiveAll(this NetMQSocket socket)
		{
			return socket.ReceiveMessages().ToList();
		}

		[Obsolete("Use ReceiveStringMessages extension method instead")]
		public static IList<string> ReceiveAllString(this NetMQSocket socket)
		{
			return socket.ReceiveStringMessages().ToList();
		}
	}
}
