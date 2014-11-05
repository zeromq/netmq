using System;
using NetMQ.Core;


namespace NetMQ.Sockets
{
    public class XPublisherSocket : NetMQSocket
    {
        internal XPublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }
    }
}
