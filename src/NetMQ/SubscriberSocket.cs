using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class SubscriberSocket : BaseSocket
    {
        public SubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
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

        public IList<byte[]> ReceiveAll()
        {
            bool hasMore;

            IList<byte[]> messages = new List<byte[]>();

            Msg msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);
            messages.Add(msg.Data);

            while (hasMore)
            {
                msg = ReceiveInternal(SendRecieveOptions.None, out hasMore);
                messages.Add(msg.Data);
            }

            return messages;
        }

        public string ReceiveString(out bool hasMore)
        {
            return ReceiveStringInternal(SendRecieveOptions.None, out hasMore);
        }

        public string ReceiveString(bool dontWait, out bool hasMore)
        {
            return ReceiveStringInternal(dontWait ? SendRecieveOptions.DontWait : SendRecieveOptions.None, out hasMore);
        }

        public void Subscribe(string topic)
        {
            base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public void Subscribe(byte[] topic)
        {
            base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public void Unsubscribe(string topic)
        {
            base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public void Unsubscribe(byte[] topic)
        {
            base.SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }
    }
}
