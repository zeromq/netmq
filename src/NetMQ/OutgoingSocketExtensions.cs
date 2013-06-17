using System.Text;

namespace NetMQ
{
	public static class OutgoingSocketExtensions
	{
		public static void Send(this IOutgoingSocket socket, byte[] data)
		{
			socket.Send(data, data.Length);
		}

		public static void Send(this IOutgoingSocket socket, string message, bool dontWait = false, bool sendMore = false)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);
			socket.Send(data, data.Length, dontWait, sendMore);
		}

		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, string message, bool dontWait = false)
		{
			socket.Send(message, false, true);
			return socket;
		}

		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, bool dontWait = false)
		{
			socket.Send(data, data.Length, dontWait, true);
			return socket;
		}

		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, int length, bool dontWait = false)
		{
			socket.Send(data, length, dontWait, true);
			return socket;
		}
	}
}
