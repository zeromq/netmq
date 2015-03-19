using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PullSocket is a NetMQSocket intended to be used as the "Pull" part of the Push-Pull pattern.
    /// This will "pull" messages that have been pushed from the "push" socket.
    /// </summary>
    public class PullSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PullSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PullSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// Don't invoke this on a PullSocket - you'll just get a NotSupportedException.
        /// </summary>
        /// <param name="msg">the Msg to transmit</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        public override void Send(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Pull socket doesn't support sending");
        }
    }
}
