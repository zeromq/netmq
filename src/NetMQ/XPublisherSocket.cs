using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class XPublisherSocket : BaseSocket
    {
        public XPublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public bool XPubVerbose
        {
            get { return GetSocketOptionX<bool>(ZmqSocketOptions.XpubVerbose); }
            set { SetSocketOption(ZmqSocketOptions.XpubVerbose, value); }
        }
    }
}
