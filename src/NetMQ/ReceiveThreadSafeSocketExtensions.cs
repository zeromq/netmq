using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMQ
{
    /// <summary>
    /// Receive methods for thread-safe sockets that support receiving
    /// </summary>
    public static class ReceiveThreadSafeSocketExtensions
    {
        #region Receiving byte array

        #region Blocking

        /// <summary>
        /// Receive a bytes from <paramref name="socket"/>, blocking until one arrives.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>The content of the received message.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static byte[] ReceiveBytes(this IThreadSafeInSocket socket,
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);
                var data = msg.CloneData();
                return data;
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
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveBytes(this IThreadSafeInSocket socket, [NotNullWhen(returnValue: true)] out byte[]? bytes)
        {
            return socket.TryReceiveBytes(TimeSpan.Zero, out bytes);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a byte-array <paramref name="socket"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="bytes">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveBytes(this IThreadSafeInSocket socket, TimeSpan timeout,
            [NotNullWhen(returnValue: true)] out byte[]? bytes, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (!socket.TryReceive(ref msg, timeout, cancellationToken))
            {
                msg.Close();
                bytes = null;
                return false;
            }

            bytes = msg.CloneData();

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
        /// <returns>The content of the received message.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<byte[]> ReceiveBytesAsync(this IThreadSafeInSocket socket,
            CancellationToken cancellationToken = default)
        {
            if (TryReceiveBytes(socket, out var bytes))
                return new ValueTask<byte[]>(bytes);

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            // and probably implement IValueTaskSource
            // TODO: should we avoid lambda here as it cause heap allocation for the environment?
            return new ValueTask<byte[]>(Task.Factory.StartNew(() => socket.ReceiveBytes(cancellationToken),
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
        public static async IAsyncEnumerable<byte[]> ReceiveBytesAsyncEnumerable(
            this IThreadSafeInSocket socket,
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
        /// <returns>The content of the received message.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static string ReceiveString(this IThreadSafeInSocket socket,
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
        /// <returns>The content of the received message.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static string ReceiveString(this IThreadSafeInSocket socket, Encoding encoding,
            CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            try
            {
                socket.Receive(ref msg, cancellationToken);
                return msg.Size > 0
                    ? msg.GetString(encoding)
                    : string.Empty;
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
        /// <param name="str">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IThreadSafeInSocket socket, [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, SendReceiveConstants.DefaultEncoding, out str);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is immediately available, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="str">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString(this IThreadSafeInSocket socket, Encoding encoding,
            [NotNullWhen(returnValue: true)] out string? str)
        {
            return socket.TryReceiveString(TimeSpan.Zero, encoding, out str);
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="str">The conent of the received message, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IThreadSafeInSocket socket, TimeSpan timeout, [NotNullWhen(returnValue: true)] out string? str,
            CancellationToken cancellationToken = default)
        {
            return socket.TryReceiveString(timeout, SendReceiveConstants.DefaultEncoding, out str, cancellationToken);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="str">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        /// <remarks>The method would return false if cancellation has had requested.</remarks>
        public static bool TryReceiveString(this IThreadSafeInSocket socket, TimeSpan timeout,
    Encoding encoding, [NotNullWhen(returnValue: true)] out string? str, CancellationToken cancellationToken = default)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, timeout, cancellationToken))
            {
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
        /// <returns>The content of the received message.</returns>
        /// <exception cref="System.OperationCanceledException">The token has had cancellation requested.</exception>
        public static ValueTask<string> ReceiveStringAsync(this IThreadSafeInSocket socket,
            CancellationToken cancellationToken = default)
        {
            if (TryReceiveString(socket, out var msg))
                return new ValueTask<string>(msg);

            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            // and probably implement IValueTaskSource
            return new ValueTask<string>(Task.Factory.StartNew(() => socket.ReceiveString(cancellationToken),
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
        public static async IAsyncEnumerable<string> ReceiveStringAsyncEnumerable(
            this IThreadSafeInSocket socket,
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