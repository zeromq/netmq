using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public abstract class DuplexSocket<T> : BaseSocket where T : DuplexSocket<T>
    {
        public DuplexSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

        protected abstract T This { get; }

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

        public T SendMore(byte[] data)
        {
            SendInternal(data, data.Length, false, true);
            return This;
        }

        public T SendMore(byte[] data, int length)
        {
            SendInternal(data, length, false, true);
            return This;
        }

        public T SendMore(byte[] data, bool dontWait)
        {
            SendInternal(data, data.Length, dontWait, true);
            return This;
        }

        public T SendMore(byte[] data, int length, bool dontWait)
        {
            SendInternal(data, length, dontWait, true);
            return This;
        }

        public T SendMore(string message)
        {
            SendInternal(message, false, true);
            return This;
        }

        public T SendMore(string message, bool dontWait)
        {
            SendInternal(message, dontWait, true);
            return This;
        }

        public byte[] Receive(out bool isMore)
        {
            var msg = ReceiveInternal(SendRecieveOptions.None, out isMore);

            return msg.Data;
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
