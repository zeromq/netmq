using System;
using System.Text;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to provide extension methods for IOutgoingSocket.
    /// </summary>
    public static class OutgoingSocketExtensions
    {
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

        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data)
        {
            socket.Send(data, data.Length);
        }

        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, bool dontWait = false)
        {
            socket.Send(data, data.Length, dontWait, true);
            return socket;
        }

        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool dontWait = false)
        {
            socket.Send(data, length, dontWait, true);
            return socket;
        }

        #endregion

        #region Sending Strings

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

        public static void Send([NotNull] this IOutgoingSocket socket, [NotNull] string message, bool dontWait = false, bool sendMore = false)
        {
            Send(socket, message, Encoding.ASCII, dontWait, sendMore);
        }

        /// <summary>
        /// Transmit a string-message of data over this socket, while indicating that more is to come
        /// (sendMore is set to true).
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

        [NotNull]
        public static IOutgoingSocket SendMore([NotNull] this IOutgoingSocket socket, [NotNull] string message, [NotNull] Encoding encoding, bool dontWait = false)
        {
            socket.Send(message, encoding, dontWait, true);
            return socket;
        }

        #endregion

        #region Sending NetMQMessage

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

        private static void Signal([NotNull] this IOutgoingSocket socket, byte status)
        {
            long signalValue = 0x7766554433221100L + status;
            var message = new NetMQMessage();
            message.Append(signalValue);

            socket.SendMessage(message);
        }

        public static void SignalOK([NotNull] this IOutgoingSocket socket)
        {
            socket.Signal(0);
        }

        public static void SignalError([NotNull] this IOutgoingSocket socket)
        {
            socket.Signal(1);
        }

        #endregion
    }
}
