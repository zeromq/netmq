using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class PublisherSocket : BaseSocket
    {
        public PublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
