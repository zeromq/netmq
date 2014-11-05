using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    public class StreamSocket : NetMQSocket
    {
        internal StreamSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

    }
}
