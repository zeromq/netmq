using System;
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

        /// <summary>
        /// Subscribe this socket to the given 'topic' - which means enable this socket to receive
        /// messages that begin with this string prefix.
        /// You can set topic to an empty string to subscribe to everything.
        /// </summary>
        /// <param name="topic">this specifies what text-prefix to subscribe to, or may be an empty-string to specify ALL</param>
        public new virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        /// <summary>
        /// Subscribe this socket to all topics - which means enable this socket to receive
        /// all messages regardless of what the string prefix is.
        /// This is the same as calling Subscribe with an empty-string for the topic.
        /// </summary>
        public virtual void SubscribeToAll()
        {
            Subscribe(String.Empty);
        }

        /// <summary>
        /// Subscribe this socket to the given 'topic' - which means enable this socket to receive
        /// messages that begin with this array of bytes.
        /// </summary>
        /// <param name="topic">this specifies what byte-array prefix to subscribe to</param>
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
