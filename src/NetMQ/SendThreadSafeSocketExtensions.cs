using System;
using System.Threading.Tasks;
using NetMQ.Utils;

namespace NetMQ
{
    /// <summary>
    /// Send extension methods for thread-safe sockets that support sending
    /// </summary>
    public static class SendThreadSafeSocketExtensions
    {
        #region Sending Byte Array

        #region Blocking

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        public static void Send(this IThreadSafeOutSocket socket, byte[] data)
        {
            Send(socket, data, data.Length);
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static void Send(this IThreadSafeOutSocket socket, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
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
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, TimeSpan timeout, byte[] data, int length)
        {
            var msg = new Msg();
            msg.InitPool(length);
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
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, TimeSpan timeout, byte[] data)
        {
            return TrySend(socket, timeout, data, data.Length);
        }

        #endregion

        #region Immediate

        /// <summary>
        /// Attempt to transmit a byte-array of data on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, byte[] data)
        {
            return TrySend(socket, TimeSpan.Zero, data);
        }

        /// <summary>
        /// Attempt to transmit a byte-array on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, byte[] data, int length)
        {
            return TrySend(socket, TimeSpan.Zero, data, length);
        }

        #endregion
        
        #region Async
        
        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        public static ValueTask SendAsync(this IThreadSafeOutSocket socket, byte[] data)
        {
            if (socket.TrySend(data))
                return new ValueTask();
            
            return new ValueTask(Task.Factory.StartNew(() => Send(socket, data), TaskCreationOptions.LongRunning));
        }

        /// <summary>
        /// Transmit a byte-array of data over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="data">the byte-array of data to send</param>
        /// <param name="length">the number of bytes to send from <paramref name="data"/>.</param>
        public static ValueTask SendAsync(this IThreadSafeOutSocket socket, byte[] data, int length)
        {
            if (socket.TrySend(data, length))
                return new ValueTask();
            
            return new ValueTask(Task.Factory.StartNew(() => Send(socket, data, length), TaskCreationOptions.LongRunning));
        }
        
        #endregion

        #endregion
        
        #region Sending Strings

        #region Blocking

        /// <summary>
        /// Transmit a string over this socket, block until message is sent.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="message">the string to send</param>
        public static void Send(this IThreadSafeOutSocket socket,  string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));

            // Encode the string into the buffer
            SendReceiveConstants.DefaultEncoding.GetBytes(message, msg);

            socket.Send(ref msg);
            msg.Close();
        }

        #endregion

        #region Timeout

        /// <summary>
        /// Attempt to transmit a single string on <paramref name="socket"/>.
        /// If message cannot be sent within <paramref name="timeout"/>, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="timeout">The maximum period of time to try to send a message.</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, TimeSpan timeout, string message)
        {
            var msg = new Msg();

            // Count the number of bytes required to encode the string.
            // Note that non-ASCII strings may not have an equal number of characters
            // and bytes. The encoding must be queried for this answer.
            // With this number, request a buffer from the pool.
            msg.InitPool(SendReceiveConstants.DefaultEncoding.GetByteCount(message));

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
        /// Attempt to transmit a single string on <paramref name="socket"/>.
        /// If message cannot be sent immediately, return <c>false</c>.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="message">the string to send</param>
        /// <returns><c>true</c> if a message was available, otherwise <c>false</c>.</returns>
        public static bool TrySend(this IThreadSafeOutSocket socket, string message)
        {
            return TrySend(socket, TimeSpan.Zero, message);
        }

        #endregion
        
        #region Async
        
        /// <summary>
        /// Transmit a string over this socket asynchronously.
        /// </summary>
        /// <param name="socket">the socket to transmit on</param>
        /// <param name="message">the string to send</param>
        public static ValueTask SendAsync(this IThreadSafeOutSocket socket, string message)
        {
            if (socket.TrySend(message))
                return new ValueTask();
            
            return new ValueTask(Task.Factory.StartNew(() => Send(socket, message), TaskCreationOptions.LongRunning));
        }
        
        #endregion

        #endregion
    }
}