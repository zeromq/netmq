using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class DealerSocket : BaseSocket
    {
        public DealerSocket(SocketBase socketHandle) : base(socketHandle)
        {
        }
    }
}
