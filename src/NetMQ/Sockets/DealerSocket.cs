using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A DealerSocket is a NetMQSocket, whereby the dealer sends messages in a way intended to achieve load-balancing
    /// - which are received in a fair queueing manner.
    /// </summary>
    public class DealerSocket : NetMQSocket
    {
        /// <summary>
        /// The type identifier for this Socket Class
        /// </summary>
        public static ZmqSocketType TypeId
        {
            get { return ZmqSocketType.Dealer; }
        }

        /// <summary>
        /// The Socket Class type identifier for this Socket
        /// </summary>
        public override ZmqSocketType SocketType
        {
            get { return PairSocket.TypeId; }
        }

        /// <summary>
        /// Create a new DealerSocket and attach socket to zero or more endpoints.               
        /// </summary>                
        /// <param name="connectionString">List of NetMQ endpoints, seperated by commas and prefixed by '@' (to bind the socket) or '>' (to connect the socket).
        /// Default action is connect (if endpoint doesn't start with '@' or '>')</param>
        /// <example><code>var socket = new DealerSocket(">tcp://127.0.0.1:5555,@127.0.0.1:55556");</code></example>                 
        public DealerSocket(string connectionString = null) : base(TypeId, connectionString, DefaultAction.Connect)
        {            
        }

        /// <summary>
        /// Create a new DealerSocket based upon the given SocketBase.
        /// </summary>
        /// <param name="socketHandle">the SocketBase to create the new socket from</param>
        internal DealerSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
