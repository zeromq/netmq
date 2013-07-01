using System;
using NetMQ;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Subscriber socket, will receive messages from publisher socket
	/// </summary>
	public class SubscriberSocket : NetMQSocket
	{
		public SubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		public override void Send(byte[] data, int length, SendReceiveOptions options)
		{
			throw new NotSupportedException("Subscriber socket doesn't support sending");
		}

        public virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        public virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }
	}
}
