using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class RequestSocket: BaseSocket
    {
        public RequestSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
