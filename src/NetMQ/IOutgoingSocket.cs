using System;

namespace NetMQ
{
    /// <summary>
    /// Interface IOutgoingSocket mandates a Send( Msg, SendReceiveOptions ) method.
    /// </summary>
    public interface IOutgoingSocket
    {
        /// <summary>
        /// Send a message if one is available within <paramref name="timeout"/>.
        /// </summary>
        /// <param name="msg">An object with message's data to send.</param>
        /// <param name="timeout">The maximum length of time to try and send a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <param name="more">Indicate if another frame is expected after this frame</param>
        /// <returns><c>true</c> if a message was sent, otherwise <c>false</c>.</returns>
        bool TrySend(ref Msg msg, TimeSpan timeout, bool more);
    }
}
