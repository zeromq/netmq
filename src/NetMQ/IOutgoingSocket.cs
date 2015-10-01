using System;

namespace NetMQ
{
    /// <summary>
    /// Interface IOutgoingSocket mandates a Send( Msg, SendReceiveOptions ) method.
    /// </summary>
    public interface IOutgoingSocket
    {
        /// <summary>
        /// Send the given Msg out upon this socket.
        /// The message content is in the form of a byte-array that Msg contains.
        /// </summary>
        /// <param name="msg">the Msg struct that contains the data and the options for this transmission</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore bits (or None)</param>
        /// <exception cref="AgainException">The send operation timed out.</exception>
        [Obsolete("Use Send(ref Msg, bool) or TrySend(ref Msg,TimeSpan, bool) instead.")]
        void Send(ref Msg msg, SendReceiveOptions options);

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
