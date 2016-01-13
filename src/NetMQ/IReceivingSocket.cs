using System;

namespace NetMQ
{
    /// <summary>
    /// Defines a socket from which message data may be read.
    /// </summary>
    public interface IReceivingSocket
    {
        /// <summary>
        /// Read message data into the given <see cref="Msg"/> object.
        /// </summary>
        /// <remarks>
        /// If a message is available, its content will be transferred to <paramref name="msg"/> and the call will
        /// return immediately.
        /// <para/>
        /// If no message is available, the behaviour of this method depends upon <paramref name="options"/>:
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="SendReceiveOptions.DontWait"/> — causes the method to immediately throw
        ///     <see cref="AgainException"/>.
        ///   </item>
        ///   <item>
        ///     <see cref="SendReceiveOptions.None"/> — blocks until the next message arrives, and cannot be
        ///     interrupted. This a convenient and safe when you know a message is available, such as for code
        ///     within a <see cref="NetMQSocket.ReceiveReady"/> callback.
        ///     <para/>
        ///     <see cref="NetMQSocket"/> objects having non-negative <see cref="SocketOptions.ReceiveTimeout"/>values
        ///     will timeout if a message is not received within the specified number of milliseconds.
        ///   </item>
        /// </list>
        /// </remarks>
        /// <param name="msg">An object to receive the message's data into.</param>
        /// <param name="options">A set of flags that control receipt. Only <see cref="SendReceiveOptions.None"/> and
        /// <see cref="SendReceiveOptions.DontWait"/> have any effect. <see cref="SendReceiveOptions.SendMore"/> makes
        /// no sense here and is ignored.</param>
        /// <exception cref="AgainException">No message was available within the allowed timeout period. Try again.
        /// </exception>
        [Obsolete("Use Receive(ref Msg) or TryReceive(ref Msg,TimeSpan) instead.")]
        void Receive(ref Msg msg, SendReceiveOptions options);

        /// <summary>
        /// Receive a message if one is available within <paramref name="timeout"/>.
        /// </summary>
        /// <param name="msg">An object to receive the message's data into.</param>
        /// <param name="timeout">The maximum length of time to wait for a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <returns><c>true</c> if a message was received, otherwise <c>false</c>.</returns>
        bool TryReceive(ref Msg msg, TimeSpan timeout);
    }
}