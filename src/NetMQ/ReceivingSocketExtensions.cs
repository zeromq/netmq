using System;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
	public static class ReceivingSocketExtensions
	{
		public static byte[] Receive(this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
		{
			return socket.Receive((options & SendReceiveOptions.DontWait) > 0, out hasMore);
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
			return socket.ReceiveString((options & SendReceiveOptions.DontWait) > 0, out hasMore);
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
	}
}
