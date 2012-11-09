using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class XSubscriberSocket : SubscriberSocket
    {
        public XSubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public void Send(byte[] data)
        {
            SendInternal(data, data.Length, false, false);
        }

        public void Send(byte[] data, int length)
        {
            SendInternal(data, length, false, false);
        }

        public void Send(byte[] data, bool dontWait)
        {
            SendInternal(data, data.Length, dontWait, false);
        }

        public void Send(byte[] data, int length, bool dontWait)
        {
            SendInternal(data, length, dontWait, false);
        }

        public void Send(string message)
        {
            SendInternal(message, false, false);
        }

        public void Send(string message, bool dontWait)
        {
            SendInternal(message, dontWait, false);
        }

        public XSubscriberSocket SendMore(byte[] data)
        {
            SendInternal(data, data.Length, false, true);
            return this;
        }

        public XSubscriberSocket SendMore(byte[] data, int length)
        {
            SendInternal(data, length, false, true);
            return this;
        }

        public XSubscriberSocket SendMore(byte[] data, bool dontWait)
        {
            SendInternal(data, data.Length, dontWait, true);
            return this;
        }

        public XSubscriberSocket SendMore(byte[] data, int length, bool dontWait)
        {
            SendInternal(data, length, dontWait, true);
            return this;
        }

        public XSubscriberSocket SendMore(string message)
        {
            SendInternal(message, false, true);
            return this;
        }

        public XSubscriberSocket SendMore(string message, bool dontWait)
        {
            SendInternal(message, dontWait, true);
            return this;
        }
    }
}
