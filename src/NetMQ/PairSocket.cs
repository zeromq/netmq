using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class PairSocket : BaseSocket
    {
        public PairSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
