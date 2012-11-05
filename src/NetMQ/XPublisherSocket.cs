using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class XPublisherSocket : BaseSocket
    {
        public XPublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
