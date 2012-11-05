using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class ResponseSocket: BaseSocket
    {
        public ResponseSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
