using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to provide extension methods for IReceivingSocket.
    /// </summary>
    public static class ReceivingSocketExtensions
    {
        #region Receiving Byte Array

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="options">the send and receive options to use</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg, options);

            var data = new byte[msg.Size];

            if (msg.Size > 0)
            {
                Buffer.BlockCopy(msg.Data, 0, data, 0, msg.Size);
            }

            hasMore = msg.HasMore;

            msg.Close();

            return data;
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="options">the send and receive options to use</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.Receive(options, out hasMore);
        }

        /// <summary>
        /// potentially non-blocking get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            var options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            return socket.Receive(options, out hasMore);
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, out bool hasMore)
        {
            return socket.Receive(false, out hasMore);
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c> within a timespan
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="timeout">a timespan to wait for arriving messages</param>
        /// <returns>a newly allocated array of bytes or <c>null</c> if no message arrived within the timeout period</returns>
        /// <exception cref="InvalidCastException">if the socket not a NetMQSocket</exception>
        [CanBeNull]
        public static byte[] Receive([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            if (!result.HasFlag(PollEvents.PollIn))
                return null;

            return socket.Receive();
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        public static byte[] Receive([NotNull] this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.Receive(false, out hasMore);
        }

        /// <summary>Receives a list of all frames of the next message, each as an array of bytes.</summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, each as an array of bytes.</returns>
        [NotNull]
        [ItemNotNull]
        public static List<byte[]> ReceiveMessages([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            var frames = new List<byte[]>(capacity: expectedFrameCount);

            bool hasMore = true;

            while (hasMore)
                frames.Add(socket.Receive(false, out hasMore));

            return frames;
        }

        #endregion

        #region Receiving Strings

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using the encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use</param>
        /// <param name="options">the send/receive options to use</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the string representation of the encoded data of the message</returns>
        /// <exception cref="ArgumentNullException">is thrown if encoding is null</exception>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, SendReceiveOptions options, out bool hasMore)
        {
            if (encoding == null)
                throw new ArgumentNullException("encoding");

            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg, options);

            hasMore = msg.HasMore;

            string data = msg.Size > 0 
                ? encoding.GetString(msg.Data, 0, msg.Size) 
                : string.Empty;

            msg.Close();

            return data;
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="options">the send/receive options to use</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            return socket.ReceiveString(Encoding.ASCII, options, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="options">the send/receive options to use</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(options, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use</param>
        /// <param name="options">the send/receive options to use</param>
        /// <returns>the string representation of the encoded data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(encoding, options, out hasMore);
        }

        /// <summary>
        /// non-blocking reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="dontWait">if true the method is non-blocking</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            return ReceiveString(socket, Encoding.ASCII, dontWait, out hasMore);
        }

        /// <summary>
        /// non-blocking reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use for the string representation</param>
        /// <param name="dontWait">if true the method is non-blocking</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, bool dontWait, out bool hasMore)
        {
            var options = SendReceiveOptions.None;

            if (dontWait)
            {
                options |= SendReceiveOptions.DontWait;
            }

            return socket.ReceiveString(encoding, options, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, out bool hasMore)
        {
            return socket.ReceiveString(false, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using the encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use for the string representation</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the string representation of the encoded data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, out bool hasMore)
        {
            return socket.ReceiveString(encoding, false, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use for the string representation</param>
        /// <returns>the string representation of the encoded data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding)
        {
            bool hasMore;
            return socket.ReceiveString(encoding, false, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        public static string ReceiveString([NotNull] this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.ReceiveString(false, out hasMore);
        }

        /// <summary>
        /// waits for a message to be read for a specified timespan
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="timeout">the time span to wait for a message to receive</param>
        /// <returns>
        /// the ASCII string representation of the data of the message or <c>null</c> if 
        /// no message arrived with in the timespan
        /// </returns>
        [CanBeNull]
        public static string ReceiveString([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            return ReceiveString(socket, Encoding.ASCII, timeout);
        }

        /// <summary>
        /// waits for a message to be read for a specified timespan
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use for the string representation</param>
        /// <param name="timeout">the time span to wait for a message to receive</param>
        /// <returns>
        /// the string representation of the encoded data of the message or <c>null</c> if 
        /// no message arrived with in the timespan
        /// </returns>
        [CanBeNull]
        public static string ReceiveString([NotNull] this NetMQSocket socket, [NotNull] Encoding encoding, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            if (!result.HasFlag(PollEvents.PollIn))
                return null;

            var msg = socket.ReceiveString(encoding);
            return msg;
        }

        /// <summary>Receives a list of all frames of the next message, decoded as ASCII strings.</summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, decoded as strings.</returns>
        [NotNull]
        [ItemNotNull]
        public static List<string> ReceiveStringMessages([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            return ReceiveStringMessages(socket, Encoding.ASCII, expectedFrameCount);
        }

        /// <summary>Receives a list of all frames of the next message, decoded as strings having the specifed <paramref name="encoding"/>.</summary>
        /// <remarks>Blocks until a message is received. The list may have one or more entries.</remarks>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding to use when converting a frame's bytes into a string.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, decoded as strings.</returns>
        [NotNull]
        [ItemNotNull]
        public static List<string> ReceiveStringMessages([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, int expectedFrameCount = 4)
        {
            var frames = new List<string>(capacity: expectedFrameCount);

            bool hasMore = true;

            while (hasMore)
                frames.Add(socket.ReceiveString(encoding, SendReceiveOptions.None, out hasMore));

            return frames;
        }

        #endregion

        #region Receiving NetMQMessge

        /// <summary>
        /// non-blocking receive of a (multipart)message and stores it in the NetMQMessage object
        /// </summary>
        /// <param name="socket">the IReceivingSocket to receive bytes from</param>
        /// <param name="message">the NetMQMessage to receive the bytes into</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        public static void ReceiveMessage([NotNull] this IReceivingSocket socket, [NotNull] NetMQMessage message, bool dontWait = false)
        {
            message.Clear();

            bool more = true;

            while (more)
            {
                byte[] buffer = socket.Receive(dontWait, out more);
                message.Append(buffer);
            }
        }

        /// <summary>
        /// non-blocking receive of a (multipart)message
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        /// <returns>the received message</returns>
        [NotNull]
        public static NetMQMessage ReceiveMessage([NotNull] this IReceivingSocket socket, bool dontWait = false)
        {
            var message = new NetMQMessage();
            socket.ReceiveMessage(message, dontWait);
            return message;
        }

        /// <summary>
        /// receive of a (multipart)message within a specified timespan
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="timeout">the timespan to wait for a message</param>
        /// <returns>the received message or <c>null</c> if non arrived within the timeout period</returns>
        [CanBeNull]
        public static NetMQMessage ReceiveMessage([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            if (!result.HasFlag(PollEvents.PollIn))
                return null;

            var msg = socket.ReceiveMessage();
            return msg;
        }

        #endregion

        #region Receiving Signals

        /// <summary>
        /// Extension-method for IReceivingSocket: repeatedly call Rece on this socket, until we receive
        /// a message with one 8-byte frame, which matches a specific pattern.
        /// </summary>
        /// <param name="socket">this socket to receive the messages from</param>
        /// <returns>true if that one frame has no bits set other than in the lowest-order byte</returns>
        public static bool WaitForSignal(this IReceivingSocket socket)
        {
            while (true)
            {
                var message = socket.ReceiveMessage();

                if (message.FrameCount == 1 && message.First.MessageSize == 8)
                {
                    long signalValue = message.First.ConvertToInt64();

                    if ((signalValue & 0x7FFFFFFFFFFFFF00L) == 0x7766554433221100L)
                    {
                        return (signalValue & 255) == 0;
                    }
                }
            }
        }

        #endregion

        [Obsolete("Use ReceiveMessages extension method instead")]
        [NotNull]
        [ItemNotNull]
        public static IList<byte[]> ReceiveAll([NotNull] this IReceivingSocket socket)
        {
            return socket.ReceiveMessages().ToList();
        }

        [Obsolete("Use ReceiveStringMessages extension method instead")]
        [NotNull]
        [ItemNotNull]
        public static IList<string> ReceiveAllString([NotNull] this IReceivingSocket socket)
        {
            return socket.ReceiveStringMessages().ToList();
        }
    }
}
