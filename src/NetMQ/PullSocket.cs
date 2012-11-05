using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class PullSocket: BaseSocket
    {
        public PullSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
