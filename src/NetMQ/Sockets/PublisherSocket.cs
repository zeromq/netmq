using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A PublisherSocket is a NetMQSocket intended to be used as the Pub in the PubSub pattern.
    /// The intended usage is for publishing messages to all subscribers which are subscribed to a given topic.
    /// </summary>
    public class PublisherSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new PublisherSocket and attach socket to zero or more endpoints.
        /// </summary>
        /// <param name="connectionString">List of NetMQ endpoints, separated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is bind (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new PublisherSocket(">tcp://127.0.0.1:5555,>127.0.0.1:55556");</code></example>
        public PublisherSocket(string connectionString = null) : base(ZmqSocketType.Pub, connectionString, DefaultAction.Bind)
        {
        }

        /// <summary>
        /// Create a new PublisherSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal PublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary><see cref="PublisherSocket"/> doesn't support sending, so this override throws <see cref="NotSupportedException"/>.</summary>
        /// <exception cref="NotSupportedException">Receive is not supported.</exception>
        public override bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            throw new NotSupportedException("PublisherSocket doesn't support receiving");
        }
    }
}
