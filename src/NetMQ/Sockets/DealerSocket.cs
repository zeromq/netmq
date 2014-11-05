using System;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Dealer socket, the dealer send messages in load balancing and receive in fair queueing.
    /// </summary>
    public class DealerSocket : NetMQSocket
    {
        internal DealerSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
