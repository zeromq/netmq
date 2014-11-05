using System;
using System.Text;
using NetMQ;
using NetMQ.Core;

namespace NetMQ.Sockets
{
    /// <summary>
    /// Subscriber socket, will receive messages from publisher socket
    /// </summary>
    public class SubscriberSocket : NetMQSocket
    {
        internal SubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public override void Send(ref Msg msg, SendReceiveOptions options)
        {
            throw new NotSupportedException("Subscriber socket doesn't support sending");
        }

        public new virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public virtual void Subscribe(string topic, Encoding encoding)
        {
            Subscribe(encoding.GetBytes(topic));
        }

        public new virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public new virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        public virtual void Unsubscribe(string topic, Encoding encoding)
        {
            Unsubscribe(encoding.GetBytes(topic));
        }

        public new virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }
    }
}
