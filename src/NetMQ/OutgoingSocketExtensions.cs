using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace NetMQ
{
    /// <summary>
    /// This static class serves to provide extension methods for IOutgoingSocket.
    /// </summary>
    public static class OutgoingSocketExtensions
    {
        /// <summary>
        /// Block until the message can be sent.
        /// </summary>
        /// <remarks>
        /// The call  blocks until the message can be sent and cannot be interrupted.
        /// Whether the message can be sent depends on the socket type.
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

        #region Blocking

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        public static void SendFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, bool more = false)
        {
            SendFrame(socket, data, data.Length, more);
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        public static void SendFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length, bool more = false)
        {
            var msg = new Msg();
            msg.InitPool(length);
            Buffer.BlockCopy(data, 0, msg.Data, msg.Offset, length);
            socket.Send(ref msg, more);
            msg.Close();
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// Send more frame, another frame must be sent after this frame. Use to chain Send methods.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMoreFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data)
        {
            SendFrame(socket, data, true);

            return socket;
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// Send more frame, another frame must be sent after this frame. Use to chain Send methods.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMoreFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length)
        {
            SendFrame(socket, data, length, true);

            return socket;
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, TimeSpan timeout, [NotNull] byte[] data, int length, bool more = false)
        {
            var msg = new Msg();
            msg.InitPool(length);
            Buffer.BlockCopy(data, 0, msg.Data, 0, length);

            if (!socket.TrySend(ref msg, timeout, more))
            {
                msg.Close();
                return false;
            }

            msg.Close();
            return true;
        }

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, TimeSpan timeout, [NotNull] byte[] data, bool more = false)
        {
            return TrySendFrame(socket, timeout, data, data.Length, more);
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data,
            bool more = false)
        {
            return TrySendFrame(socket, TimeSpan.Zero, data, more);
        }

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, [NotNull] byte[] data, int length,
           bool more = false)
        {
            return TrySendFrame(socket, TimeSpan.Zero, data, length, more);
        }

        #endregion

        #endregion

        #region Sending a multipart message as byte arrays

        #region Blocking

        /// <summary>
        /// Send multiple frames on <paramref name="socket"/>, blocking until all frames are sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="frames">frames to transmit</param>
        public static void SendMultipartBytes([NotNull] this IOutgoingSocket socket, params byte[][] frames)
        {
            SendMultipartBytes(socket, (IEnumerable<byte[]>)frames);
        }

        /// <summary>
        /// Send multiple frames on <paramref name="socket"/>, blocking until all frames are sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="frames">frames to transmit</param>
        public static void SendMultipartBytes([NotNull] this IOutgoingSocket socket, IEnumerable<byte[]> frames)
        {
            var enumerator = frames.GetEnumerator();

            try
            {
                // move to the first element, if false frames is empty
                if (!enumerator.MoveNext())
                {
                    throw new ArgumentException("frames is empty", nameof(frames));
                }

                var current = enumerator.Current;

                // we always one item back to make sure we send the last frame without the more flag
                while (enumerator.MoveNext())
                {
                    // this is a more frame
                    socket.SendMoreFrame(current);

                    current = enumerator.Current;
                }

                // sending the last frame
                socket.SendFrame(current);
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a multiple frames on <paramref name="socket"/>.
        /// If frames cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="frames">frames to transmit</param>
        public static bool TrySendMultipartBytes([NotNull] this IOutgoingSocket socket, TimeSpan timeout, params byte[][] frames)
        {
            return TrySendMultipartBytes(socket, timeout, (IEnumerable<byte[]>)frames);
        }

        /// <summary>
        /// Attempt to transmit a multiple frames on <paramref name="socket"/>.
        /// If frames cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="frames">frames to transmit</param>
        public static bool TrySendMultipartBytes([NotNull] this IOutgoingSocket socket, TimeSpan timeout,
            IEnumerable<byte[]> frames)
        {
            var enumerator = frames.GetEnumerator();

            try
            {
                // move to the first element, if false frames is empty
                if (!enumerator.MoveNext())
                {
                    throw new ArgumentException("frames is empty", nameof(frames));
                }

                var current = enumerator.Current;

                // only the first frame need to be sent with a timeout
                if (!enumerator.MoveNext())
                {
                    return socket.TrySendFrame(timeout, current);
                }
                else
                {
                    bool sentSuccessfully = socket.TrySendFrame(timeout, current, true);

                    if (!sentSuccessfully)
                        return false;
                }

                // fetching the second frame
                current = enumerator.Current;

                // we always one item back to make sure we send the last frame without the more flag
                while (enumerator.MoveNext())
                {
                    // this is a more frame
                    socket.SendMoreFrame(current);

                    current = enumerator.Current;
                }

                // sending the last frame
                socket.SendFrame(current);

                return true;
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a multiple frames on <paramref name="socket"/>.
        /// If frames cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="frames">frames to transmit</param>
        public static bool TrySendMultipartBytes([NotNull] this IOutgoingSocket socket, params byte[][] frames)
        {
            return TrySendMultipartBytes(socket, TimeSpan.Zero, (IEnumerable<byte[]>)frames);
        }

        /// <summary>
        /// Attempt to transmit a multiple frames on <paramref name="socket"/>.
        /// If frames cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="frames">frames to transmit</param>
        public static bool TrySendMultipartBytes([NotNull] this IOutgoingSocket socket, IEnumerable<byte[]> frames)
        {
            return TrySendMultipartBytes(socket, TimeSpan.Zero, frames);
        }

        #endregion

        #endregion

        #region Sending Strings

        #region Blocking

        /// <summary>
        /// Transmit a string over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">the string to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        public static void SendFrame([NotNull] this IOutgoingSocket socket, [NotNull] string message, bool more = false)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));

            // Encode the string into the buffer
            SendReceiveConstants.DefaultEncoding.GetBytes(message, 0, message.Length, msg.Data, 0);

            socket.Send(ref msg, more);
            msg.Close();
        }

        /// <summary>
        /// Transmit a string over this socket, block until frame is sent.
        /// Send more frame, another frame must be sent after this frame. Use to chain Send methods.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">the string to send</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMoreFrame([NotNull] this IOutgoingSocket socket, [NotNull] string message)
        {
            SendFrame(socket, message, true);

            return socket;
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a single string frame on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="message">the string to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, TimeSpan timeout, [NotNull] string message, bool more = false)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));

            // Encode the string into the buffer
            SendReceiveConstants.DefaultEncoding.GetBytes(message, 0, message.Length, msg.Data, 0);

            if (!socket.TrySend(ref msg, timeout, more))
            {
                msg.Close();
                return false;
            }

            msg.Close();
            return true;
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a single string frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">the string to send</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrame([NotNull] this IOutgoingSocket socket, [NotNull] string message, bool more = false)
        {
            return TrySendFrame(socket, TimeSpan.Zero, message, more);
        }

        #endregion

        #endregion

        #region Sending a multipart message as NetMQMessage

        #region Blocking

        /// <summary>
        /// Send multiple message on <paramref name="socket"/>, blocking until all entire message is sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">message to transmit</param>
        public static void SendMultipartMessage([NotNull] this IOutgoingSocket socket, [NotNull] NetMQMessage message)
        {
            if (message.FrameCount == 0)
                throw new ArgumentException("message is empty", nameof(message));

            for (int i = 0; i < message.FrameCount - 1; i++)
            {
                socket.SendMoreFrame(message[i].Buffer, message[i].MessageSize);
            }

            socket.SendFrame(message.Last.Buffer, message.Last.MessageSize);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a multiple message on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="message">message to transmit</param>
        public static bool TrySendMultipartMessage([NotNull] this IOutgoingSocket socket, TimeSpan timeout, [NotNull] NetMQMessage message)
        {
            if (message.FrameCount == 0)
                throw new ArgumentException("message is empty", nameof(message));
            else if (message.FrameCount == 1)
            {
                return TrySendFrame(socket, timeout, message[0].Buffer, message[0].MessageSize);
            }
            else
            {
                bool sentSuccessfully = TrySendFrame(socket, timeout, message[0].Buffer, message[0].MessageSize, true);
                if (!sentSuccessfully)
                    return false;
            }

            for (int i = 1; i < message.FrameCount - 1; i++)
            {
                socket.SendMoreFrame(message[i].Buffer, message[i].MessageSize);
            }

            socket.SendFrame(message.Last.Buffer, message.Last.MessageSize);

            return true;
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a multiple message on <paramref name="socket"/>.
        /// If frames cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="message">message to transmit</param>
        public static bool TrySendMultipartMessage([NotNull] this IOutgoingSocket socket, [NotNull] NetMQMessage message)
        {
            return TrySendMultipartMessage(socket, TimeSpan.Zero, message);
        }

        #endregion

        #endregion

        #region Sending an empty frame

        #region Blocking

        /// <summary>
        /// Transmit an empty frame over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        public static void SendFrameEmpty([NotNull] this IOutgoingSocket socket, bool more = false)
        {
            SendFrame(socket, EmptyArray<byte>.Instance, more);
        }


        /// <summary>
        /// Transmit an empty frame over this socket, block until frame is sent.
        /// Send more frame, another frame must be sent after this frame. Use to chain Send methods.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <returns>a reference to this IOutgoingSocket so that method-calls may be chained together</returns>
        [NotNull]
        public static IOutgoingSocket SendMoreFrameEmpty([NotNull] this IOutgoingSocket socket)
        {
            SendFrame(socket, EmptyArray<byte>.Instance, true);

            return socket;
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit an empty frame on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrameEmpty([NotNull] this IOutgoingSocket socket, TimeSpan timeout, bool more = false)
        {
            return TrySendFrame(socket, timeout, EmptyArray<byte>.Instance, more);
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit an empty frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="more">set this flag to true to signal that you will be immediately sending another frame (optional: default is false)</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySendFrameEmpty([NotNull] this IOutgoingSocket socket, bool more = false)
        {
            return TrySendFrame(socket, EmptyArray<byte>.Instance, more);
        }

        #endregion

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

            Msg msg = new Msg();
            msg.InitPool(8);
            NetworkOrderBitsConverter.PutInt64(signalValue, msg.Data);

            socket.Send(ref msg, false);

            msg.Close();
        }

        /// <summary>
        /// Attempt to transmit a status-signal over this socket.
        /// If signal cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        /// <param name="status">a byte that contains the status signal to send</param>
        private static bool TrySignal([NotNull] this IOutgoingSocket socket, byte status)
        {
            long signalValue = 0x7766554433221100L + status;

            Msg msg = new Msg();
            msg.InitPool(8);
            NetworkOrderBitsConverter.PutInt64(signalValue, msg.Data);

            if (!socket.TrySend(ref msg, TimeSpan.Zero, false))
            {
                msg.Close();
                return false;
            }

            msg.Close();
            return true;
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
        /// Attempt to transmit a specific status-signal over this socket that indicates OK.
        /// If signal cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        public static bool TrySignalOK([NotNull] this IOutgoingSocket socket)
        {
            return TrySignal(socket, 0);
        }

        /// <summary>
        /// Transmit a specific status-signal over this socket that indicates there is an error.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        public static void SignalError([NotNull] this IOutgoingSocket socket)
        {
            socket.Signal(1);
        }

        /// <summary>
        /// Attempt to transmit a specific status-signal over this socket that indicates there is an error.
        /// If signal cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the IOutgoingSocket to transmit on</param>
        public static bool TrySignalError([NotNull] this IOutgoingSocket socket)
        {
            return socket.TrySignal(1);
        }

        #endregion
    }
}
