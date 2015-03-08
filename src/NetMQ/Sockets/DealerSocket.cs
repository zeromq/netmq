using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A DealerSocket is a NetMQSocket, whereby the dealer sends messages in a way intended to achieve load-balancing
    /// - which are received in a fair queueing manner.
    /// </summary>
    public class DealerSocket : NetMQSocket
    {
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
