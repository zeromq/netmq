using System;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

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
        /// <returns>The content of the received message.</returns>
        [NotNull]
        public static byte[] ReceiveBytes([NotNull] this IThreadSafeInSocket socket)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg);

            var data = msg.CloneData();

            msg.Close();
            return data;
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
        public static bool TryReceiveBytes([NotNull] this IThreadSafeInSocket socket, out byte[] bytes)
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
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveBytes([NotNull] this IThreadSafeInSocket socket, TimeSpan timeout, out byte[] bytes)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (!socket.TryReceive(ref msg, timeout))
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
        /// <returns>The content of the received message.</returns>
        public static ValueTask<byte[]> ReceiveBytesAsync([NotNull] this IThreadSafeInSocket socket)
        {
            if (TryReceiveBytes(socket, out var bytes))
                return new ValueTask<byte[]>(bytes);
            
            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            // and probably implement IValueTaskSource
            return new ValueTask<byte[]>(Task.Factory.StartNew(socket.ReceiveBytes, TaskCreationOptions.LongRunning));
        }
        
        #endregion
        
        #endregion
        
        #region Receiving string

        #region Blocking

        /// <summary>
        /// Receive a string from <paramref name="socket"/>, blocking until one arrives, and decode using <see cref="SendReceiveConstants.DefaultEncoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <returns>The content of the received message.</returns>
        public static string ReceiveString([NotNull] this IThreadSafeInSocket socket)
        {
            return socket.ReceiveString(SendReceiveConstants.DefaultEncoding);
        }

        /// <summary>
        /// Receive a string from <paramref name="socket"/>, blocking until one arrives, and decode using <paramref name="encoding"/>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <returns>The content of the received message.</returns>
        public static string ReceiveString([NotNull] this IThreadSafeInSocket socket, [NotNull] Encoding encoding)
        {
            var msg = new Msg();
            msg.InitEmpty();

            socket.Receive(ref msg);
            
            var str = msg.Size > 0
                ? msg.GetString(encoding)
                : string.Empty;

            msg.Close();
            return str;
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
        public static bool TryReceiveString([NotNull] this IThreadSafeInSocket socket, out string str)
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
        public static bool TryReceiveString([NotNull] this IThreadSafeInSocket socket, [NotNull] Encoding encoding, out string str)
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
        /// <param name="str">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString([NotNull] this IThreadSafeInSocket socket, TimeSpan timeout, out string str)
        {
            return socket.TryReceiveString(timeout, SendReceiveConstants.DefaultEncoding, out str);
        }

        /// <summary>
        /// Attempt to receive a string from <paramref name="socket"/>, and decode using <paramref name="encoding"/>.
        /// If no message is available within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">The socket to receive from.</param>
        /// <param name="timeout">The maximum period of time to wait for a message to become available.</param>
        /// <param name="encoding">The encoding used to convert the data to a string.</param>
        /// <param name="str">The content of the received message, or <c>null</c> if no message was available.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TryReceiveString([NotNull] this IThreadSafeInSocket socket, TimeSpan timeout, [NotNull] Encoding encoding, out string str)
        {
            var msg = new Msg();
            msg.InitEmpty();

            if (socket.TryReceive(ref msg, timeout))
            {
                str = msg.Size > 0
                    ? msg.GetString(encoding)
                    : string.Empty;

                msg.Close();
                return true;
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
        /// <returns>The content of the received message.</returns>
        public static ValueTask<string> ReceiveStringAsync([NotNull] this IThreadSafeInSocket socket)
        {
            if (TryReceiveString(socket, out var msg))
                return new ValueTask<string>(msg);
                    
            // TODO: this is a hack, eventually we need kind of IO ThreadPool for thread-safe socket to wait on asynchronously
            // and probably implement IValueTaskSource
            return new ValueTask<string>(Task.Factory.StartNew(socket.ReceiveString, TaskCreationOptions.LongRunning));
        }

        #endregion

        #endregion
    }
}