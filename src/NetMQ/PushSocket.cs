using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class PushSocket : BaseSocket
    {
        public PushSocket(SocketBase socketHandle)
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

        public PushSocket SendMore(byte[] data)
        {
            SendInternal(data, data.Length, false, true);
            return this;
        }

        public PushSocket SendMore(byte[] data, int length)
        {
            SendInternal(data, length, false, true);
            return this;
        }

        public PushSocket SendMore(byte[] data, bool dontWait)
        {
            SendInternal(data, data.Length, dontWait, true);
            return this;
        }

        public PushSocket SendMore(byte[] data, int length, bool dontWait)
        {
            SendInternal(data, length, dontWait, true);
            return this;
        }

        public PushSocket SendMore(string message)
        {
            SendInternal(message, false, true);
            return this;
        }

        public PushSocket SendMore(string message, bool dontWait)
        {
            SendInternal(message, dontWait, true);
            return this;
        }
    }
}
