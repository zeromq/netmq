using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public abstract class BaseSocket
    {
        SocketBase m_socketHandle;

        public BaseSocket(SocketBase socketHandle)
        {
            m_socketHandle = socketHandle;
        }
    }
}
