using System;

namespace NetMQ
{
    /// <summary>
    /// Defines a socket from which message data may be read.
    /// </summary>
    public interface IReceivingSocket
    {
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