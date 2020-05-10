using System.Diagnostics;

namespace NetMQ
{
    /// <summary>
    /// Thread-safe socket extension methods
    /// </summary>
    public static class ThreadSafeSocketExtensions
    {
        /// <summary>
        /// Block until the message can be sent.
        /// </summary>
        /// <remarks>
        /// The call  blocks until the message can be sent and cannot be interrupted.
        /// Whether the message can be sent depends on the socket type.
        /// </remarks>
        /// <param name="socket">Socket to transmit on</param>
        /// <param name="msg">An object with message's data to send.</param>
        public static void Send(this IThreadSafeSocket socket, ref Msg msg)
        {
            var result = socket.TrySend(ref msg, SendReceiveConstants.InfiniteTimeout);
            Debug.Assert(result);
        }

        /// <summary>
        /// Block until the next message arrives, then make the message's data available via <paramref name="msg"/>.
        /// </summary>
        /// <remarks>
        /// The call  blocks until the next message arrives, and cannot be interrupted. This a convenient and safe when
        /// you know a message is available, such as for code within a <see cref="NetMQSocket.ReceiveReady"/> callback.
        /// </remarks>
        /// <param name="socket">Socket to transmit on</param>
        /// <param name="msg">An object to receive the message's data into.</param>
        public static void Receive(this IThreadSafeSocket socket, ref Msg msg)
        {
            var result = socket.TryReceive(ref msg, SendReceiveConstants.InfiniteTimeout);
            Debug.Assert(result);
        }
    }
}