// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System.Text;


namespace NetMQ
{
	public static class OutgoingSocketExtensions
	{
        /// <summary>
        /// Transmit a byte-array of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
		public static void Send(this IOutgoingSocket socket, byte[] data)
		{
			socket.Send(data, data.Length);
		}


#if !PRE_4

        /// <summary>
        /// Transmit a string-message of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <param name="sendMore">if true, this indicates that this is one part of a multi-part message and more parts are coming</param>
		public static void Send(this IOutgoingSocket socket, string message, bool dontWait = false, bool sendMore = false)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);
			socket.Send(data, data.Length, dontWait, sendMore);
		}

#else

        /// <summary>
        /// Transmit a string-message of data over this socket,
        /// with the default value of false supplied for the dontWait and sendMore parameters.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <param name="sendMore">if true, this indicates that this is one part of a multi-part message and more parts are coming</param>
		public static void Send(this IOutgoingSocket socket, string message)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);
			socket.Send(data, data.Length, false, false);
		}

        /// <summary>
        /// Transmit a string-message of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <param name="sendMore">if true, this indicates that this is one part of a multi-part message and more parts are coming</param>
		public static void Send(this IOutgoingSocket socket, string message, bool dontWait, bool sendMore)
		{
			byte[] data = Encoding.ASCII.GetBytes(message);
			socket.Send(data, data.Length, dontWait, sendMore);
		}
#endif


        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (sendMore is set to true) and the default value of false supplied for the dontWait parameter.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, string message)
		{
			socket.Send(message, false, true);
			return socket;
		}

        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (sendMore is set to true).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, string message, bool dontWait)
		{
            //TODO: Note - it seemed that the dontWait argument was hard-coded to true in the existing code,
            //      and the dontWait argument-value was unused. Assuming here that that was a mistake - but should be verified. jh
            socket.Send(message, dontWait, true);
			return socket;
		}

        /// <summary>
        /// Transmit a byte-array of data over this socket, while indicating that more is to come
        /// (sendMore is set to true). This waits for the operation to return (false is assumed for the dontWait parameter).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data)
		{
			socket.Send(data, data.Length, false, true);
			return socket;
		}

        /// <summary>
        /// Transmit a byte-array of data over this socket, while indicating that more is to come
        /// (sendMore is set to true). This waits for the operation to return unless dontWait is set to true.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, bool dontWait)
		{
			socket.Send(data, data.Length, dontWait, true);
			return socket;
		}

        /// <summary>
        /// Transmit a specified number of bytes of a byte-array of data over this socket, while indicating that more is to come
        /// (sendMore is set to true). This waits for the operation to return (false is assumed for the dontWait parameter).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to transmit</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, int length)
		{
			socket.Send(data, length, false, true);
			return socket;
		}

        /// <summary>
        /// Transmit a specified number of bytes of a byte-array of data over this socket, while indicating that more is to come
        /// (sendMore is set to true). This waits for the operation to return unless dontWait is set to true.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes of data to transmit</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
		public static IOutgoingSocket SendMore(this IOutgoingSocket socket, byte[] data, int length, bool dontWait)
		{
			socket.Send(data, length, dontWait, true);
			return socket;
		}
	}
}
