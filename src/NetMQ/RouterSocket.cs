using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class RouterSocket : BaseSocket
    {
        public RouterSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
