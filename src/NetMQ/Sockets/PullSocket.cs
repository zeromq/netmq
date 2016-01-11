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
        /// Create a new PullSocket and attach socket to zero or more endpoints.               
        /// </summary>                
        /// <param name="connectionString">List of NetMQ endpoints, seperated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is bind (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new PullSocket(">tcp://127.0.0.1:5555,>127.0.0.1:55556");</code></example>               
        public PullSocket(string connectionString = null) : base(ZmqSocketType.Pull, connectionString, DefaultAction.Bind)
        {

        }

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
        /// <exception cref="NotSupportedException">Send must not be called on a PullSocket.</exception>
        [Obsolete("Use Send(ref Msg, bool) or TrySend(ref Msg,TimeSpan, bool) instead.")]
        public override void Send(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Pull socket doesn't support sending");
        }

        public override bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            throw new NotSupportedException("Pull socket doesn't support sending");
        }
    }
}
