using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Router socket, the first message is always the identity of the sender
    /// </summary>
    public class RouterSocket : NetMQSocket
    {
        /// <summary>
        /// The type identifier for this Socket Class
        /// </summary>
        public static ZmqSocketType TypeId
        {
            get { return ZmqSocketType.Router; }
        }

        /// <summary>
        /// The Socket Class type identifier for this Socket
        /// </summary>
        public override ZmqSocketType SocketType
        {
            get { return PairSocket.TypeId; }
        }

        /// <summary>
        /// Create a new RouterSocket and attach socket to zero or more endpoints.               
        /// </summary>                
        /// <param name="connectionString">List of NetMQ endpoints, seperated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is bind (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new RouterSocket(">tcp://127.0.0.1:5555,>127.0.0.1:55556");</code></example>  
        public RouterSocket(string connectionString = null) : base(TypeId, connectionString, DefaultAction.Bind)
        {
            
        }

        /// <summary>
        /// Create a new RouterSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal RouterSocket(SocketBase socketHandle) : base(socketHandle)
        {
        }
    }
}
