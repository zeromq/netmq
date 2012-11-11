using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class XPublisherSocket : PublisherSocket
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

        public byte[] Receive(bool dontWait, out bool isMore)
        {
            var msg = ReceiveInternal(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out isMore);

            return msg.Data;
        }

        public string ReceiveString(out bool hasMore)
        {
            return ReceiveStringInternal(SendRecieveOptions.None, out hasMore);
        }

        public string ReceiveString(bool dontWait, out bool hasMore)
        {
            return ReceiveStringInternal(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out hasMore);
        }

        public IList<byte[]> ReceiveAll()
        {
            return base.ReceiveAllInternal();
        }

        public IList<string> ReceiveAllString()
        {
            return base.ReceiveAllStringInternal();
        }

    }
}
