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
    /// Send and Receive extensions for sockets with RoutingId capability (ServerSocket)
    /// </summary>
    public static class RoutingIdSocketExtensions
    {
        #region Sending Byte Array

        #region Blocking

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        public static void Send(this IRoutingIdSocket socket, uint routingId, byte[] data)
        {
            Send(socket, routingId, data, data.Length);
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until frame is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static void Send(this IRoutingIdSocket socket, uint routingId, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
            msg.RoutingId = routingId;
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
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, TimeSpan timeout, uint routingId, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
            msg.RoutingId = routingId;
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
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, TimeSpan timeout, uint routingId, byte[] data)
        {
            return TrySend(socket, timeout, routingId, data, data.Length);
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a byte-array of data on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, uint routingId, byte[] data)
        {
            return TrySend(socket, TimeSpan.Zero, routingId, data);
        }

        /// <summary>
        /// Attempt to transmit a single frame on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, uint routingId, byte[] data, int length)
        {
            return TrySend(socket, TimeSpan.Zero, routingId, data, length);
        }

        #endregion

        #region Async

        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        public static ValueTask SendAsync(this IRoutingIdSocket socket, uint routingId, byte[] data)
        {
            if (socket.TrySend(routingId, data))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, routingId, data),
                TaskCreationOptions.LongRunning));
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static ValueTask SendAsync(this IRoutingIdSocket socket, uint routingId, byte[] data, int length)
        {
            if (socket.TrySend(routingId, data, length))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, routingId, data, length),
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
        /// <param name="routingId">Routing id</param>
        /// <param name="message">the string to send</param>
        public static void Send(this IRoutingIdSocket socket, uint routingId, string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));
            msg.RoutingId = routingId;

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
        /// <param name="routingId">Routing id</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, TimeSpan timeout, uint routingId, string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));
            msg.RoutingId = routingId;

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
        /// <param name="routingId">Routing id</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IRoutingIdSocket socket, uint routingId, string message)
        {
            return TrySend(socket, TimeSpan.Zero, routingId, message);
        }

        #endregion

        #region Async

        /// <summary>
        /// Transmit a string over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="message">the string to send</param>
        public static ValueTask SendAsync(this IRoutingIdSocket socket, uint routingId, string message)
        {
            if (socket.TrySend(routingId, message))
                return new ValueTask();

            return new ValueTask(Task.Factory.StartNew(() => Send(socket, routingId, message),
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
        /// <returns>Tuple of routing id and received bytes</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (uint, byte[]) ReceiveBytes(this IRoutingIdSocket socket, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);
                var data = msg.CloneData();
                var routingId = msg.RoutingId;
                return (routingId, data);
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
        /// <param name="routingId">Routing id</param>
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveBytes(this IRoutingIdSocket socket, out uint routingId, [NotNullWhen(returnValue: true)] out byte[]? bytes)
        {
            return socket.TryReceiveBytes(TimeSpan.Zero, out routingId, out bytes);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a byte-array <paramref name="socket"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveBytes(this IRoutingIdSocket socket, TimeSpan timeout, out uint routingId,
            [NotNullWhen(returnValue: true)] out byte[]? bytes, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (!socket.TryReceive(ref msg, timeout, cancellationToken))
            {
                msg.Close();
                bytes = null;
                routingId = 0;
                return false;
            }

            bytes = msg.CloneData();
            routingId = msg.RoutingId;

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
        /// <returns>Tuple of routing id and received bytes</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<(uint, byte[])> ReceiveBytesAsync(this IRoutingIdSocket socket, CancellationToken cancellationToken = default)
        {
            if (TryReceiveBytes(socket, out var routingId, out var bytes))
                return new ValueTask<(uint, byte[])>((routingId, bytes));

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            return new ValueTask<(uint, byte[])>(Task.Factory.StartNew(() => socket.ReceiveBytes(cancellationToken),
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
        public static async IAsyncEnumerable<(uint, byte[])> ReceiveBytesAsyncEnumerable(
            this IRoutingIdSocket socket,
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
        /// <returns>Tuple of routing id and the content of the received message as a string.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (uint, string) ReceiveString(this IRoutingIdSocket socket, CancellationToken cancellationToken = default)
        {
            return socket.ReceiveString(SendReceiveConstants.DefaultEncoding, cancellationToken);
        }

        /// <summary>
        /// Receive a string from <paramref name="socket"/>, blocking until one arrives, and decode using <paramref name="encoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>Tuple of routing id and the content of the received message as a string.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static (uint, string) ReceiveString(this IRoutingIdSocket socket, Encoding encoding, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);          
                var routingId = msg.RoutingId;
                var str = msg.Size > 0
                    ? msg.GetString(encoding)
                    : string.Empty;
                return (routingId, str);
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
        /// <param name="routingId">Routing id</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IRoutingIdSocket socket, out uint routingId,
            [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, SendReceiveConstants.DefaultEncoding, out routingId, out str);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is immediately available, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IRoutingIdSocket socket, Encoding encoding, out uint routingId,
            [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, encoding, out routingId, out str);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IRoutingIdSocket socket, TimeSpan timeout,
            out uint routingId, [NotNullWhen(returnValue: true)] out string? str,
            CancellationToken cancellationToken = default)
        {
            return socket.TryReceiveString(timeout, SendReceiveConstants.DefaultEncoding, out routingId, out str, cancellationToken);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="routingId">Routing id</param>
        /// <param name="str">The content of the received message as a string, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IRoutingIdSocket socket, TimeSpan timeout,
            Encoding encoding, out uint routingId, [NotNullWhen(returnValue: true)] out string? str, 
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, timeout, cancellationToken))
            {
                routingId = msg.RoutingId;

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
            routingId = 0;
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
        /// <returns>Tuple of routing id and a string</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<(uint, string)> ReceiveStringAsync(this IRoutingIdSocket socket,
            CancellationToken cancellationToken = default)
        {
            if (TryReceiveString(socket, out var routingId, out var msg))
                return new ValueTask<(uint, string)>((routingId, msg));

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            return new ValueTask<(uint, string)>(Task.Factory.StartNew(() => socket.ReceiveString(cancellationToken),
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
        public static async IAsyncEnumerable<(uint, string)> ReceiveStringAsyncEnumerable(
            this IRoutingIdSocket socket,
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