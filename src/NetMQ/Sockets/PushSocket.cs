using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PushSocket is a NetMQSocket intended to be used as the "Push" part of the Push-Pull pattern.
    /// This will "push" messages to be pulled by the "pull" socket.
    /// </summary>
    public class PushSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PushSocket and attach socket to zero or more endpoints.
        /// </summary>
        /// <param name="connectionString">List of NetMQ endpoints, separated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is connect (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new PushSocket(">tcp://127.0.0.1:5555,@tcp://127.0.0.1:55556");</code></example>
        public PushSocket(string connectionString = null) : base(ZmqSocketType.Push, connectionString, DefaultAction.Connect)
        {
        }

        /// <summary>
        /// Create a new PushSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PushSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary><see cref="PushSocket"/> doesn't support sending, so this override throws <see cref="NotSupportedException"/>.</summary>
        /// <exception cref="NotSupportedException">Receive is not supported.</exception>
        public override bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            throw new NotSupportedException("PushSocket doesn't support receiving");
        }
    }
}
