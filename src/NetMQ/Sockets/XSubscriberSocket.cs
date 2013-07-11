using NetMQ.zmq;

namespace NetMQ.Sockets
{
	public class XSubscriberSocket : NetMQSocket
	{
		public XSubscriberSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

        public new virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public new virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        public new virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        public new virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }
	}
}
