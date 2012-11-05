using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class XSubscriberSocket : BaseSocket
    {
        public XSubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
