using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Utils;

namespace NetMQ
{
    /// <summary>
    /// Send and Receive extensions for sockets with group capability (ServerSocket)
    /// </summary>
    public static class GroupSocketExtensions
    {
        #region Sending Byte Array

        #region Blocking

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        public static void Send(this IGroupOutSocket socket, string group, byte[] data)
        {
            Send(socket, group, data, data.Length);
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static void Send(this IGroupOutSocket socket, string group, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
            msg.Group = group;
            data.Slice(0, length).CopyTo(msg);
            socket.Send(ref msg);
            msg.Close();
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a byte-array of data on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, TimeSpan timeout, string group, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
            msg.Group = group;
            data.CopyTo(msg);
            if (!socket.TrySend(ref msg, timeout))
            {
                msg.Close();
                return false;
            }

            msg.Close();
            return true;
        }

        /// <summary>
        /// Attempt to transmit a byte-array of data on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, TimeSpan timeout, string group, byte[] data)
        {
            return TrySend(socket, timeout, group, data, data.Length);
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a byte-array of data on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, string group, byte[] data)
        {
            return TrySend(socket, TimeSpan.Zero, group, data);
        }

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, string group, byte[] data, int length)
        {
            return TrySend(socket, TimeSpan.Zero, group, data, length);
        }

        #endregion

        #region Async

        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        public static ValueTask SendAsync(this IGroupOutSocket socket, string group, byte[] data)
        {
            if (socket.TrySend(group, data))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, group, data),
                TaskCreationOptions.LongRunning));
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static ValueTask SendAsync(this IGroupOutSocket socket, string group, byte[] data, int length)
        {
            if (socket.TrySend(group, data, length))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, group, data, length),
                TaskCreationOptions.LongRunning));
        }

        #endregion

        #endregion

        #region Sending Strings

        #region Blocking

        /// <summary>
        /// Transmit a string over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="message">the string to send</param>
        public static void Send(this IGroupOutSocket socket, string group, string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));
            msg.Group = group;

            // Encode the string into the buffer
            SendReceiveConstants.DefaultEncoding.GetBytes(message, msg);

            socket.Send(ref msg);
            msg.Close();
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a single string frame on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, TimeSpan timeout, string group, string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));
            msg.Group = group;

            // Encode the string into the buffer
            SendReceiveConstants.DefaultEncoding.GetBytes(message, msg);

            if (!socket.TrySend(ref msg, timeout))
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
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IGroupOutSocket socket, string group, string message)
        {
            return TrySend(socket, TimeSpan.Zero, group, message);
        }

        #endregion

        #region Async

        /// <summary>
        /// Transmit a string over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="group">The group to send the message to.</param>
        /// <param name="message">the string to send</param>
        public static ValueTask SendAsync(this IGroupOutSocket socket, string group, string message)
        {
            if (socket.TrySend(group, message))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, group, message),
                TaskCreationOptions.LongRunning));
        }

        #endregion

        #endregion

        #region Receiving byte array

        #region Blocking

        /// <summary>
        /// Receive a bytes from <paramref name="socket"/>, blocking until one arrives.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of group and received bytes</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (string, byte[]) ReceiveBytes(this IGroupInSocket socket,
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);
                var data = msg.CloneData();
                var group = msg.Group;
                return (group, data);
            }
            finally
            {
                msg.Close();
            }
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to receive a byte-array <paramref name="socket"/>.
        /// If no message is immediately available, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="group">The message group</param>
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveBytes(this IGroupInSocket socket,
            [NotNullWhen(returnValue: true)] out string? group, [NotNullWhen(returnValue: true)] out byte[]? bytes)
        {
            return socket.TryReceiveBytes(TimeSpan.Zero, out group, out bytes);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a byte-array <paramref name="socket"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="group">The message group.</param>
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveBytes(this IGroupInSocket socket, TimeSpan timeout,
            [NotNullWhen(returnValue: true)] out string? group,
            [NotNullWhen(returnValue: true)] out byte[]? bytes, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (!socket.TryReceive(ref msg, timeout, cancellationToken))
            {
                msg.Close();
                bytes = null;
                group = null;
                return false;
            }

            bytes = msg.CloneData();
            group = msg.Group;

            msg.Close();
            return true;
        }

        #endregion

        #region Async

        /// <summary>
        /// Receive a bytes from <paramref name="socket"/> asynchronously.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of group and received bytes</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<(string, byte[])> ReceiveBytesAsync(this IGroupInSocket socket,
            CancellationToken cancellationToken = default)
        {
            if (TryReceiveBytes(socket, out var group, out var bytes))
                return new ValueTask<(string, byte[])>((group, bytes));

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            return new ValueTask<(string, byte[])>(Task.Factory.StartNew(() => socket.ReceiveBytes(cancellationToken),
                cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default));
        }

        #endregion

        #region AsyncEnumerable

#if NETSTANDARD2_1
        /// <summary>
        /// Provides a consuming IAsyncEnumerable for receiving messages from the socket.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>An IAsyncEnumerable that receive and returns messages from the socket.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static async IAsyncEnumerable<(string, byte[])> ReceiveBytesAsyncEnumerable(
            this IGroupInSocket socket,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (true)
            {
                yield return await socket.ReceiveBytesAsync(cancellationToken);
            }
        }

#endif

        #endregion

        #endregion

        #region Receiving string

        #region Blocking

        /// <summary>
        /// Receive a string from <paramref name="socket"/>, blocking until one arrives, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of group and the content of the received message as a string.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (string, string) ReceiveString(this IGroupInSocket socket,
            CancellationToken cancellationToken = default)
        {
            return socket.ReceiveString(SendReceiveConstants.DefaultEncoding, cancellationToken);
        }

        /// <summary>
        /// Receive a string from <paramref name="socket"/>, blocking until one arrives, and decode using <paramref name="encoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of group and the content of the received message as a string.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (string, string) ReceiveString(this IGroupInSocket socket, Encoding encoding,
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);
                var group = msg.Group;
                var str = msg.Size > 0
                    ? msg.GetString(encoding)
                    : string.Empty;
                return (group, str);
            }
            finally
            {
                msg.Close();
            }
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// If no message is immediately available, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="group">The message group.</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IGroupInSocket socket,
            [NotNullWhen(returnValue: true)] out string? group,
            [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, SendReceiveConstants.DefaultEncoding, out group, out str);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is immediately available, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="group">The message group.</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IGroupInSocket socket, Encoding encoding, 
            [NotNullWhen(returnValue: true)] out string? group,
            [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, encoding, out group, out str);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="group">The message group</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IGroupInSocket socket, TimeSpan timeout,
            [NotNullWhen(returnValue: true)] out string? group, 
            [NotNullWhen(returnValue: true)] out string? str,
            CancellationToken cancellationToken = default)
        {
            return socket.TryReceiveString(timeout, SendReceiveConstants.DefaultEncoding, out group, out str,
                cancellationToken);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="group">The message group</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IGroupInSocket socket, TimeSpan timeout,
            Encoding encoding, 
            [NotNullWhen(returnValue: true)] out string? group, 
            [NotNullWhen(returnValue: true)] out string? str,
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, timeout, cancellationToken))
            {
                group = msg.Group;

                try
                {
                    str = msg.Size > 0
                        ? msg.GetString(encoding)
                        : string.Empty;
                    return true;
                }
                finally
                {
                    msg.Close();
                }
            }

            str = null;
            group = null;
            msg.Close();
            return false;
        }

        #endregion

        #region Async

        /// <summary>
        /// Receive a string from <paramref name="socket"/> asynchronously.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of group and a string</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<(string, string)> ReceiveStringAsync(this IGroupInSocket socket,
            CancellationToken cancellationToken = default)
        {
            if (TryReceiveString(socket, out var group, out var msg))
                return new ValueTask<(string, string)>((group, msg));

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            return new ValueTask<(string, string)>(Task.Factory.StartNew(() => socket.ReceiveString(cancellationToken),
                cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default));
        }

        #endregion

        #region AsyncEnumerable

#if NETSTANDARD2_1
        /// <summary>
        /// Provides a consuming IAsyncEnumerable for receiving messages from the socket.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>An IAsyncEnumerable that receive and returns messages from the socket.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static async IAsyncEnumerable<(string, string)> ReceiveStringAsyncEnumerable(
            this IGroupInSocket socket,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (true)
            {
                yield return await socket.ReceiveStringAsync(cancellationToken);
            }
        }

#endif

        #endregion

        #endregion
    }
}
