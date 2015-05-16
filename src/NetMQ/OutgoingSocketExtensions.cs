using System;
using System.Text;
using JetBrains.Annotations;
using System.Diagnostics;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to provide extension methods for IOutgoingSocket.
    /// </summary>
    public static class OutgoingSocketExtensions
    {

		/// <summary>
		/// Block until the message is can be sent.
		/// </summary>
		/// <remarks>
		/// The call  blocks until the message can be sent and cannot be interrupted. 
		/// Wether the message can be sent depends on the socket type.
		/// </remarks>
		/// <param name="socket">The socket to send the message on.</param>
		/// <param name="msg">An object with message's data to send.</param>
		/// <param name="more">Indicate if another frame is expected after this frame</param>
		public static void Send(this IOutgoingSocket socket, ref Msg msg, bool more)
		{
			var result = socket.TrySend(ref msg, SendReceiveConstants.InfiniteTimeout, more);
			Debug.Assert(result);
		}
			
        #region Sending Byte Array

        /// <summary>
        /// Transmit a byte-array of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <param name="options">options to control how the data is sent</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length, SendReceiveOptions options)
        {
            var msg = new Msg();
            msg.InitPool(length);

            Buffer.BlockCopy(data, 0, msg.Data, 0, length);

            socket.Send(ref msg, options);

            msg.Close();
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <param name="sendMore">set this flag to true to signal that you will be immediately sending another message (optional: default is false)</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool dontWait = false, bool sendMore = false)
        {
            var options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            if (sendMore)
            {
                options |= SendReceiveOptions.SendMore;
            }

            socket.Send(data, length, options);
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data)
        {
            socket.Send(data, data.Length);
        }

        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (the SendMore flag is set to true).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, bool dontWait = false)
        {
            socket.Send(data, data.Length, dontWait, true);
            return socket;
        }

        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (the SendMore flag is set to true).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool dontWait = false)
        {
            socket.Send(data, length, dontWait, true);
            return socket;
        }

        #endregion

        #region Sending Strings

        /// <summary>
        /// Transmit a string-message of data over this socket. The string will be encoded into bytes using the specified Encoding.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="encoding">the Encoding to use when converting the message-string into bytes</param>
        /// <param name="options">use this to specify which of the DontWait and SendMore flags to set</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, SendReceiveOptions options)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(encoding.GetByteCount(message));

            // Encode the string into the buffer
            encoding.GetBytes(message, 0, message.Length, msg.Data, 0);

            socket.Send(ref msg, options);

            msg.Close();
        }

        /// <summary>
        /// Transmit a string-message of data over this socket. The string will be encoded into bytes using the specified Encoding.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="encoding">the Encoding to use when converting the message-string into bytes</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <param name="sendMore">set this flag to true to signal that you will be immediately sending another message (optional: default is false)</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, bool dontWait = false, bool sendMore = false)
        {
            var options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            if (sendMore)
            {
                options |= SendReceiveOptions.SendMore;
            }

            socket.Send(message, encoding, options);
        }

        /// <summary>
        /// Transmit a string-message of data over this socket. The string will be encoded into bytes using the default Encoding (ASCII).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <param name="sendMore">set this flag to true to signal that you will be immediately sending another message (optional: default is false)</param>
        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] string message, bool dontWait = false, bool sendMore = false)
        {
            Send(socket, message, Encoding.ASCII, dontWait, sendMore);
        }

        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (the SendMore flag is set to true).
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] string message, bool dontWait = false)
        {
            socket.Send(message, dontWait, true);
            return socket;
        }

        /// <summary>
        /// Transmit a string-message of data over this socket and also signal that you are sending more.
        /// The string will be encoded into bytes using the specified Encoding.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">a string containing the message to send</param>
        /// <param name="encoding">the Encoding to use when converting the message-string into bytes</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, bool dontWait = false)
        {
            socket.Send(message, encoding, dontWait, true);
            return socket;
        }

        #endregion

        #region Sending NetMQMessage

        /// <summary>
        /// Transmit a message over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">the NetMQMessage that contains the frames of data to send</param>
        /// <param name="dontWait">if true, return immediately without waiting for the send operation to complete (optional: default is false)</param>
        public static void SendMessage([NotNull] this IOutgoingSocket socket, [NotNull] NetMQMessage message, bool dontWait = false)
        {
            for (int i = 0; i < message.FrameCount - 1; i++)
            {
                socket.Send(message[i].Buffer, message[i].MessageSize, dontWait, true);
            }

            socket.Send(message.Last.Buffer, message.Last.MessageSize, dontWait);
        }

        #endregion

        #region Sending Signals

        /// <summary>
        /// Transmit a status-signal over this socket.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="status">a byte that contains the status signal to send</param>
        private static void Signal([NotNull] this IOutgoingSocket socket, byte status)
        {
            long signalValue = 0x7766554433221100L + status;
            var message = new NetMQMessage();
            message.Append(signalValue);

            socket.SendMessage(message);
        }

        /// <summary>
        /// Transmit a specific status-signal over this socket that indicates OK.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        public static void SignalOK([NotNull] this IOutgoingSocket socket)
        {
            socket.Signal(0);
        }

        /// <summary>
        /// Transmit a specific status-signal over this socket that indicates there is an error.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        public static void SignalError([NotNull] this IOutgoingSocket socket)
        {
            socket.Signal(1);
        }

        #endregion
    }
}
