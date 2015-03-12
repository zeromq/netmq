using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// Provides extension methods for the <see cref="IReceivingSocket"/> interface,
    /// via which messages may be received in several ways.
    /// </summary>
    public static class ReceivingSocketExtensions
    {
        private static readonly Encoding s_defaultEncoding = Encoding.UTF8;

        #region Receiving a frame as a byte array

        #region Blocking

        [NotNull]
        public static byte[] ReceiveFrameBytes([NotNull] this IReceivingSocket socket)
        {
            bool more;
            return socket.ReceiveFrameBytes(out more);
        }

        [NotNull]
        public static byte[] ReceiveFrameBytes([NotNull] this IReceivingSocket socket, out bool more)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg);

            var data = msg.CloneData();

            more = msg.HasMore;

            msg.Close();

            return data;
        }

        #endregion

        #region Immediate

        public static bool TryReceiveFrameBytes([NotNull] this IReceivingSocket socket, out byte[] bytes)
        {
            bool more;
            return socket.TryReceiveFrameBytes(out bytes, out more);
        }

        public static bool TryReceiveFrameBytes([NotNull] this IReceivingSocket socket, out byte[] bytes, out bool more)
        {
            return socket.TryReceiveFrameBytes(TimeSpan.Zero, out bytes, out more);
        }

        #endregion

        #region Timeout

        public static bool TryReceiveFrameBytes([NotNull] this IReceivingSocket socket, TimeSpan timeout, out byte[] bytes)
        {
            bool more;
            return socket.TryReceiveFrameBytes(timeout, out bytes, out more);
        }

        public static bool TryReceiveFrameBytes([NotNull] this IReceivingSocket socket, TimeSpan timeout, out byte[] bytes, out bool more)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (!socket.TryReceive(ref msg, timeout))
            {
                bytes = null;
                more = false;
                return false;
            }

            bytes = msg.CloneData();
            more = msg.HasMore;

            msg.Close();

            return true;
        }

        #endregion

        #region Obsolete

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.Receive(SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, out bool hasMore)
        {
            return socket.Receive(SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// potentially non-blocking get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            return socket.Receive(dontWait ? SendReceiveOptions.DontWait : SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="options">a SendReceiveOptions that can specify the DontWait or SendMore flags</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.Receive(options, out hasMore);
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c>
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="options">a SendReceiveOptions which can specify the DontWait or SendMore flags (or None)</param>
        /// <param name="hasMore"><c>true</c> when more parts of a multipart message are available</param>
        /// <returns>a newly allocated array of bytes</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg, options);

            var data = msg.CloneData();

            hasMore = msg.HasMore;

            msg.Close();

            return data;
        }

        /// <summary>
        /// get the data section of the available message as <c>byte[]</c> within a timespan
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="timeout">a timespan to wait for arriving messages</param>
        /// <returns>a newly allocated array of bytes or <c>null</c> if no message arrived within the timeout period</returns>
        /// <exception cref="InvalidCastException">if the socket not a NetMQSocket</exception>
        [CanBeNull]
        [Obsolete("Use ReceiveFrameBytes or TryReceiveFrameBytes instead.")]
        public static byte[] Receive([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            return result.HasFlag(PollEvents.PollIn) 
                ? socket.Receive() 
                : null;
        }

        #endregion

        #endregion

        #region Receiving a multipart message as byte arrays

        #region Blocking

        [NotNull]
        public static List<byte[]> ReceiveMultipartBytes([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            var frames = new List<byte[]>(expectedFrameCount);
            socket.ReceiveMultipartBytes(ref frames);
            return frames;
        }

        public static void ReceiveMultipartBytes([NotNull] this IReceivingSocket socket, ref List<byte[]> frames, int expectedFrameCount = 4)
        {
            if (frames == null)
                frames = new List<byte[]>(expectedFrameCount);

            var msg = new Msg();

            var more = true;
            while (more)
            {
                msg.InitEmpty();
                socket.Receive(ref msg);
                frames.Add(msg.CloneData());
                more = msg.HasMore;
            }

            // ReSharper disable once ExceptionNotDocumented
            msg.Close();
        }

        #endregion

        #region Immediate

        public static bool TryReceiveMultipartBytes([NotNull] this IReceivingSocket socket, ref List<byte[]> frames, int expectedFrameCount = 4)
        {
            return socket.TryReceiveMultipartBytes(TimeSpan.Zero, ref frames, expectedFrameCount);
        }

        #endregion

        #region Timeout

        public static bool TryReceiveMultipartBytes([NotNull] this IReceivingSocket socket, TimeSpan timeout, ref List<byte[]> frames, int expectedFrameCount = 4)
        {
            var msg = new Msg();
            msg.InitEmpty();

            // Try to read the first frame
            if (!socket.TryReceive(ref msg, timeout))
                return false;

            // We have one, so prepare the container
            if (frames == null)
                frames = new List<byte[]>(expectedFrameCount);
            else
                frames.Clear();

            // Add the frame
            frames.Add(msg.CloneData());

            // Rinse and repeat...
            while (msg.HasMore)
            {
                msg.InitEmpty();
                socket.Receive(ref msg);
                frames.Add(msg.CloneData());
            }

            return true;
        }

        #endregion

        #region Obsolete

        /// <summary>Receives a list of all frames of the next message, each as an array of bytes.</summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, each as an array of bytes.</returns>
        [Obsolete("Use ReceiveMultipartBytes or TryReceiveMultipartBytes instead")]
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

        [Obsolete("Use ReceiveMultipartBytes or TryReceiveMultipartBytes instead")]
        [NotNull]
        [ItemNotNull]
        public static IList<byte[]> ReceiveAll([NotNull] this IReceivingSocket socket)
        {
            return socket.ReceiveMessages().ToList();
        }

        #endregion

        #endregion

        #region Receiving a frame as a string

        #region Blocking

        [NotNull]
        public static string ReceiveFrameString([NotNull] this IReceivingSocket socket)
        {
            bool more;
            return socket.ReceiveFrameString(s_defaultEncoding, out more);
        }

        [NotNull]
        public static string ReceiveFrameString([NotNull] this IReceivingSocket socket, out bool more)
        {
            return socket.ReceiveFrameString(s_defaultEncoding, out more);
        }

        [NotNull]
        public static string ReceiveFrameString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding)
        {
            bool more;
            return socket.ReceiveFrameString(encoding, out more);
        }

        [NotNull]
        public static string ReceiveFrameString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, out bool more)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg);

            more = msg.HasMore;

            var str = msg.Size > 0
                ? encoding.GetString(msg.Data, 0, msg.Size)
                : string.Empty;

            msg.Close();

            return str;
        }

        #endregion

        #region Immediate

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, out string frameString)
        {
            bool more;
            return socket.TryReceiveFrameString(TimeSpan.Zero, s_defaultEncoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, out string frameString, out bool more)
        {
            return socket.TryReceiveFrameString(TimeSpan.Zero, s_defaultEncoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, out string frameString)
        {
            bool more;
            return socket.TryReceiveFrameString(TimeSpan.Zero, encoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, out string frameString, out bool more)
        {
            return socket.TryReceiveFrameString(TimeSpan.Zero, encoding, out frameString, out more);
        }

        #endregion

        #region Timeout

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, TimeSpan timeout, out string frameString)
        {
            bool more;
            return socket.TryReceiveFrameString(timeout, s_defaultEncoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, TimeSpan timeout, out string frameString, out bool more)
        {
            return socket.TryReceiveFrameString(timeout, s_defaultEncoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, TimeSpan timeout, [NotNull] Encoding encoding, out string frameString)
        {
            bool more;
            return socket.TryReceiveFrameString(timeout, encoding, out frameString, out more);
        }

        public static bool TryReceiveFrameString([NotNull] this IReceivingSocket socket, TimeSpan timeout, [NotNull] Encoding encoding, out string frameString, out bool more)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, timeout))
            {
                more = msg.HasMore;

                frameString = msg.Size > 0
                    ? encoding.GetString(msg.Data, 0, msg.Size)
                    : string.Empty;

                msg.Close();

                return true;
            }

            frameString = null;
            more = false;
            return false;
        }

        #endregion

        #region Obsolete

        /// <summary>
        /// Read an available message, extract the data which is converted to a string
        /// using the specified Encoding, and inform of the availability of more messages via the hasMore parameter.
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the Encoding to use</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore flags</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the message data encoded as a string</returns>
        /// <exception cref="ArgumentNullException">encoding must not be null.</exception>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
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
        /// Read an available message, extract the data which is converted to a string
        /// using the default Encoding.ASCII, and inform of the availability of more messages via the hasMore parameter.
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore flags</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the message data encoded as a string - using the ASCII-encoding</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, SendReceiveOptions options, out bool hasMore)
        {
            return socket.ReceiveString(Encoding.ASCII, options, out hasMore);
        }

        /// <summary>
        /// Read an available message, and extract the data which is converted to a string
        /// using ASCII as the Encoding.
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore flags</param>
        /// <returns>the message data encoded a a string using the ASCII-encoding</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, SendReceiveOptions options)
        {
            bool hasMore;
            return socket.ReceiveString(Encoding.ASCII, options, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore flags</param>
        /// <returns>the message data encoded as a string</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
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
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, bool dontWait, out bool hasMore)
        {
            return ReceiveString(socket, Encoding.ASCII, dontWait ? SendReceiveOptions.DontWait : SendReceiveOptions.None, out hasMore);
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
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, bool dontWait, out bool hasMore)
        {
            return socket.ReceiveString(encoding, dontWait ? SendReceiveOptions.DontWait : SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using ASCII as encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="hasMore">true if more messages are available, false otherwise</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, out bool hasMore)
        {
            return socket.ReceiveString(Encoding.ASCII, SendReceiveOptions.None, out hasMore);
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
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, out bool hasMore)
        {
            return socket.ReceiveString(encoding, SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <param name="encoding">the encoding to use for the string representation</param>
        /// <returns>the string representation of the encoded data of the message</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding)
        {
            bool hasMore;
            return socket.ReceiveString(encoding, SendReceiveOptions.None, out hasMore);
        }

        /// <summary>
        /// reads an available message and extracts the data which is converted to a string
        /// using encoding and informs about the availability of more messages
        /// </summary>
        /// <param name="socket">the socket to read from</param>
        /// <returns>the ASCII string representation of the data of the message</returns>
        [NotNull]
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this IReceivingSocket socket)
        {
            bool hasMore;
            return socket.ReceiveString(Encoding.ASCII, SendReceiveOptions.None, out hasMore);
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
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            return socket.ReceiveString(Encoding.ASCII, timeout);
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
        [Obsolete("Use ReceiveFrameString or TryReceiveFrameString methods instead")]
        public static string ReceiveString([NotNull] this NetMQSocket socket, [NotNull] Encoding encoding, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            return result.HasFlag(PollEvents.PollIn) 
                ? socket.ReceiveString(encoding) 
                : null;
        }

        #endregion

        #endregion

        #region Receiving a multipart message as strings

        #region Blocking

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="expectedFrameCount"></param>
        [NotNull]
        public static List<string> ReceiveMultipartStrings([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            return ReceiveMultipartStrings(socket, s_defaultEncoding, expectedFrameCount);
        }

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="encoding"></param>
        /// <param name="expectedFrameCount"></param>
        [NotNull]
        public static List<string> ReceiveMultipartStrings([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, int expectedFrameCount = 4)
        {
            var frames = new List<string>(expectedFrameCount);
            var more = true;
            while (more)
                frames.Add(socket.ReceiveFrameString(encoding, out more));
            return frames;
        }

        #endregion

        #region Immediate

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="frames"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartStrings([NotNull] this IReceivingSocket socket, [CanBeNull] ref List<string> frames, int expectedFrameCount = 4)
        {
            return TryReceiveMultipartStrings(socket, s_defaultEncoding, ref frames, expectedFrameCount);
        }

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="encoding"></param>
        /// <param name="frames"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartStrings([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, [CanBeNull] ref List<string> frames, int expectedFrameCount = 4)
        {
            return socket.TryReceiveMultipartStrings(TimeSpan.Zero, encoding, ref frames, expectedFrameCount);
        }

        #endregion

        #region Timeout

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="timeout"></param>
        /// <param name="frames"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartStrings([NotNull] this IReceivingSocket socket, TimeSpan timeout, [CanBeNull] ref List<string> frames, int expectedFrameCount = 4)
        {
            return TryReceiveMultipartStrings(socket, timeout, s_defaultEncoding, ref frames, expectedFrameCount);
        }

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="timeout"></param>
        /// <param name="encoding"></param>
        /// <param name="frames"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartStrings([NotNull] this IReceivingSocket socket, TimeSpan timeout, [NotNull] Encoding encoding, [CanBeNull] ref List<string> frames, int expectedFrameCount = 4)
        {
            var msg = new Msg();
            msg.InitEmpty();

            // Try to read the first frame
            if (!socket.TryReceive(ref msg, timeout))
                return false;

            // We have one, so prepare the container
            if (frames == null)
                frames = new List<string>(expectedFrameCount);
            else
                frames.Clear();

            // Add the frame
            frames.Add(encoding.GetString(msg.Data, 0, msg.Size));

            // Rinse and repeat...
            while (msg.HasMore)
            {
                msg.InitEmpty();
                socket.Receive(ref msg);
                frames.Add(encoding.GetString(msg.Data, 0, msg.Size));
            }

            return true;
        }

        #endregion

        #region Obsolete

        /// <summary>Receives a list of all frames of the next message, decoded as ASCII strings.</summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, decoded as strings.</returns>
        [Obsolete("Use ReceiveMultipartStrings or TryReceiveMultipartStrings instead")]
        [NotNull]
        [ItemNotNull]
        public static List<string> ReceiveStringMessages([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            return ReceiveStringMessages(socket, Encoding.ASCII, expectedFrameCount);
        }

        /// <summary>Receives a list of all frames of the next message, decoded as strings having the specified <paramref name="encoding"/>.</summary>
        /// <remarks>Blocks until a message is received. The list may have one or more entries.</remarks>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding to use when converting a frame's bytes into a string.</param>
        /// <param name="expectedFrameCount">Specifies the initial capacity of the <see cref="List{T}"/> used
        /// to buffer results. If the number of frames is known, set it here. If more frames arrive than expected,
        /// an extra allocation will occur, but the result will still be correct.</param>
        /// <returns>A list of all frames of the next message, decoded as strings.</returns>
        [Obsolete("Use ReceiveMultipartStrings or TryReceiveMultipartStrings instead")]
        [NotNull]
        [ItemNotNull]
        public static List<string> ReceiveStringMessages([NotNull] this IReceivingSocket socket, [NotNull] Encoding encoding, int expectedFrameCount = 4)
        {
            var frames = new List<string>(capacity: expectedFrameCount);

            var more = true;
            while (more)
                frames.Add(socket.ReceiveString(encoding, out more));

            return frames;
        }

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <exception cref="AgainException">If <paramref name="socket"/> is a <see cref="NetMQSocket"/> and
        /// <see cref="SocketOptions.ReceiveTimeout"/> elapsed.</exception>
        [Obsolete("Use ReceiveMultipartStrings or TryReceiveMultipartStrings instead")]
        [NotNull]
        [ItemNotNull]
        public static IList<string> ReceiveAllString([NotNull] this IReceivingSocket socket)
        {
            return socket.ReceiveStringMessages().ToList();
        }

        #endregion

        #endregion

        #region Receiving a multipart message as NetMQMessage

        #region Blocking

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="expectedFrameCount"></param>
        [NotNull]
        public static NetMQMessage ReceiveMultipartMessage([NotNull] this IReceivingSocket socket, int expectedFrameCount = 4)
        {
            var msg = new Msg();
            var message = new NetMQMessage(expectedFrameCount);

            do
            {
                msg.InitEmpty();
                socket.Receive(ref msg);
                message.Append(msg.CloneData());
            }
            while (msg.HasMore);

            return message;
        }

        #endregion

        #region Immediate

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="message"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartMessage([NotNull] this IReceivingSocket socket, [CanBeNull] ref NetMQMessage message, int expectedFrameCount = 4)
        {
            return socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref message, expectedFrameCount);
        }

        #endregion

        #region Timeout

        /// <summary></summary>
        /// <param name="socket">The <see cref="IReceivingSocket"/> to receive from.</param>
        /// <param name="timeout"></param>
        /// <param name="message"></param>
        /// <param name="expectedFrameCount"></param>
        public static bool TryReceiveMultipartMessage([NotNull] this IReceivingSocket socket, TimeSpan timeout, [CanBeNull] ref NetMQMessage message, int expectedFrameCount = 4)
        {
            var msg = new Msg();
            msg.InitEmpty();

            // Try to read the first frame
            if (!socket.TryReceive(ref msg, timeout))
                return false;

            // We have one, so prepare the container
            if (message == null)
                message = new NetMQMessage(expectedFrameCount);
            else
                message.Clear();

            // Add the frame
            message.Append(new NetMQFrame(msg.CloneData()));

            // Rinse and repeat...
            while (msg.HasMore)
            {
                msg.InitEmpty();
                socket.Receive(ref msg);
                message.Append(new NetMQFrame(msg.CloneData()));
            }

            return true;
        }

        #endregion

        #region Obsolete

        /// <summary>
        /// non-blocking receive of a (multipart)message and stores it in the <see cref="NetMQMessage"/> object
        /// </summary>
        /// <param name="socket">the <see cref="IReceivingSocket"/> to receive bytes from</param>
        /// <param name="message">the NetMQMessage to receive the bytes into</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        [Obsolete("Use ReceiveMultipartMessage or TryReceiveMultipartMessage instead")]
        public static void ReceiveMessage([NotNull] this IReceivingSocket socket, [NotNull] NetMQMessage message, bool dontWait = false)
        {
            message.Clear();

            var more = true;
            while (more)
                message.Append(socket.Receive(dontWait, out more));
        }

        /// <summary>
        /// non-blocking receive of a (multipart)message
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="dontWait">non-blocking if <c>true</c> and blocking otherwise</param>
        /// <returns>the received message</returns>
        [Obsolete("Use ReceiveMultipartMessage or TryReceiveMultipartMessage instead")]
        [NotNull]
        public static NetMQMessage ReceiveMessage([NotNull] this IReceivingSocket socket, bool dontWait = false)
        {
            var message = new NetMQMessage();
            var more = true;
            while (more)
                message.Append(socket.Receive(dontWait, out more));
            return message;
        }

        /// <summary>
        /// receive of a (multipart)message within a specified timespan
        /// </summary>
        /// <param name="socket">the socket to use</param>
        /// <param name="timeout">the timespan to wait for a message</param>
        /// <returns>the received message or <c>null</c> if non arrived within the timeout period</returns>
        [Obsolete("Use ReceiveMultipartMessage or TryReceiveMultipartMessage instead")]
        [CanBeNull]
        public static NetMQMessage ReceiveMessage([NotNull] this NetMQSocket socket, TimeSpan timeout)
        {
            var result = socket.Poll(PollEvents.PollIn, timeout);

            return result.HasFlag(PollEvents.PollIn) 
                ? socket.ReceiveMessage() 
                : null;
        }

        #endregion

        #endregion

        #region Receiving a signal

        #region Blocking

        /// <summary></summary>
        /// <param name="socket">The <see cref="NetMQSocket"/> to receive from.</param>
        /// <returns><c>true</c> if the received signal was zero, otherwise <c>false</c>.</returns>
        public static bool ReceiveSignal([NotNull] this IReceivingSocket socket)
        {
            var msg = new Msg();
            msg.InitEmpty();

            while (true)
            {
                socket.Receive(ref msg);

                var isMultiFrame = msg.HasMore;
                while (msg.HasMore)
                {
                    msg.InitEmpty();
                    socket.Receive(ref msg);
                }

                if (isMultiFrame || msg.Size != 8)
                    continue;

                var signalValue = NetworkOrderBitsConverter.ToInt64(msg.Data);

                if ((signalValue & 0x7FFFFFFFFFFFFF00L) == 0x7766554433221100L)
                    return (signalValue & 255) == 0;
            }
        }

        #endregion

        #region Immediate

        public static bool TryReceiveSignal([NotNull] this IReceivingSocket socket, out bool signal)
        {
            return socket.TryReceiveSignal(TimeSpan.Zero, out signal);
        }

        #endregion

        #region Timeout

        public static bool TryReceiveSignal([NotNull] this IReceivingSocket socket, TimeSpan timeout, out bool signal)
        {
            var msg = new Msg();
            msg.InitEmpty();

            // TODO use clock to enforce timeout across multiple loop iterations — if invalid messages are received regularly, the method may not return once the timeout elapses

            while (true)
            {
                if (!socket.TryReceive(ref msg, timeout))
                {
                    signal = false;
                    return false;
                }

                var isMultiFrame = msg.HasMore;
                while (msg.HasMore)
                {
                    msg.InitEmpty();
                    socket.Receive(ref msg);
                }

                if (isMultiFrame || msg.Size != 8)
                    continue;

                var signalValue = NetworkOrderBitsConverter.ToInt64(msg.Data);

                if ((signalValue & 0x7FFFFFFFFFFFFF00L) == 0x7766554433221100L)
                {
                    signal = (signalValue & 255) == 0;
                    return true;
                }
            }
        }

        #endregion

        #region Obsolete

        /// <summary>
        /// Extension-method for IReceivingSocket: repeatedly call ReceiveMessage on this socket, until we receive
        /// a message with one 8-byte frame, which matches a specific pattern.
        /// </summary>
        /// <param name="socket">this socket to receive the messages from</param>
        /// <returns>true if that one frame has no bits set other than in the lowest-order byte</returns>
        public static bool WaitForSignal([NotNull] this IReceivingSocket socket)
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

        #endregion
    }
}
