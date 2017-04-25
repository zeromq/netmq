using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// This is a NetMQSocket but provides no additional functionality.
    /// You can use it when you need an instance that is a NetMQSocket
    /// but with none of the distinguishing behavior of any of the other socket types.
    /// </summary>
    /// <remarks>
    /// This is provided because NetMQSocket is an abstract class, so you cannot instantiate it directly.
    /// </remarks>
    public class StreamSocket : NetMQSocket
    {
        /// <summary>
        /// Create a new StreamSocket and attach socket to zero or more endpoints.
        /// </summary>
        /// <param name="connectionString">List of NetMQ endpoints, separated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is connect (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new StreamSocket(">tcp://127.0.0.1:5555,@127.0.0.1:55556");</code></example>
        public StreamSocket(string connectionString = null) : base(ZmqSocketType.Stream, connectionString, DefaultAction.Connect)
        {
        }

        /// <summary>
        /// Create a new StreamSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal StreamSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
