using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public static class ReceivingSocketExtensions
	{
		public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
		{
			return socket.Receive(options.HasFlag(SendReceiveOptions.DontWait), out hasMore);
		}

		public static byte[] Receive(this IReceivingSocket socket, out bool hasMore)
		{
			return socket.Receive(false, out hasMore);
		}

		public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options)
		{
			bool hasMore;
			return socket.Receive(options, out hasMore);
		}

		public static byte[] Receive(this IReceivingSocket socket)
		{
			bool hasMore;
			return socket.Receive(false, out hasMore);
		}

		public static string ReceiveString(this IReceivingSocket socket, bool dontWait, out bool hasMore)
		{
			byte[] data = socket.Receive(dontWait, out hasMore);
			return Encoding.ASCII.GetString(data);
		}

		public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
		{
			return socket.ReceiveString(options.HasFlag(SendReceiveOptions.DontWait), out hasMore);
		}

		public static string ReceiveString(this IReceivingSocket socket, SendReceiveOptions options)
		{
			bool hasMore;
			return socket.ReceiveString(options, out hasMore);
		}

		public static string ReceiveString(this IReceivingSocket socket, out bool hasMore)
		{
			return socket.ReceiveString(false, out hasMore);
		}

		public static string ReceiveString(this IReceivingSocket socket)
		{
			bool hasMore;
			return socket.ReceiveString(false, out hasMore);
		}

		public static NetMQMessage ReceiveMessage(this IReceivingSocket socket, bool dontWait = false)
		{
			NetMQMessage message = new NetMQMessage();
			socket.ReceiveMessage(message, dontWait);
			return message;
		}

		public static NetMQMessage ReceiveMessage(this NetMQSocket socket, TimeSpan timeout) 
		{
			var item = new PollItem(socket.SocketHandle, PollEvents.PollIn);
			var items = new[] { item };
			ZMQ.Poll(items, (int)timeout.TotalMilliseconds);

			if (item.ResultEvent.HasFlag(PollEvents.PollError) && !socket.IgnoreErrors)
				throw new ErrorPollingException("Error while polling", socket);

			if (!item.ResultEvent.HasFlag(PollEvents.PollIn))
				return null;

			var msg = socket.ReceiveMessage();
			return msg;
		}

		public static void ReceiveMessage(this IReceivingSocket socket, NetMQMessage message, bool dontWait = false)
		{
			message.Clear();

			bool more = true;

			while (more)
			{
				byte[] buffer = socket.Receive(dontWait, out more);
				message.Append(buffer);
			}
		}

		public static IEnumerable<byte[]> ReceiveMessages(this IReceivingSocket socket)
		{
			bool hasMore = true;

			while (hasMore)
				yield return socket.Receive(false, out hasMore);
		}

		public static IEnumerable<string> ReceiveStringMessages(this IReceivingSocket socket)
		{
			bool hasMore = true;

			while (hasMore)
				yield return socket.ReceiveString(SendReceiveOptions.None, out hasMore);
		}

		[Obsolete("Use ReceiveMessages extension method instead")]
		public static IList<byte[]> ReceiveAll(this IReceivingSocket socket)
		{
			return socket.ReceiveMessages().ToList();
		}

		[Obsolete("Use ReceiveStringMessages extension method instead")]
		public static IList<string> ReceiveAllString(this IReceivingSocket socket)
		{
			return socket.ReceiveStringMessages().ToList();
		}
	}
}
