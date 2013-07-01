using NetMQ.zmq;

namespace NetMQ.Sockets
{
	public class XSubscriberSocket : NetMQSocket
	{
		public XSubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
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
