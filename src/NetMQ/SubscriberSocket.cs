using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class SubscriberSocket : BaseSocket
    {
        public SubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
