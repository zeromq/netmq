using System;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// A SubscriberSocket is a NetMQSocket intended to be used as the "Sub" in the PubSub pattern.
    /// The intended usage is to receive messages from the  publisher socket.
    /// </summary>
    public class SubscriberSocket : NetMQSocket
    {
        internal SubscriberSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// Don't invoke this on a SubscriberSocket - you'll just get a NotSupportedException.
        /// </summary>
        /// <param name="msg">the Msg to transmit</param>
        /// <param name="options">a SendReceiveOptions that may be None, or any of the bits DontWait, SendMore</param>
        public override void Send(ref Msg msg, SendReceiveOptions options)
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
        /// Subscribe this socket to the given 'topic' - which means enable this socket to receive
        /// messages that begin with this string prefix, using the given Encoding.
        /// You can set topic to an empty string to subscribe to everything.
        /// </summary>
        /// <param name="topic">this specifies what text-prefix to subscribe to, or may be an empty-string to specify ALL</param>
        /// <param name="encoding">the character-Encoding to use when converting the topic string internally into a byte-array</param>
        public virtual void Subscribe(string topic, Encoding encoding)
        {
            Subscribe(encoding.GetBytes(topic));
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

        /// <summary>
        /// Subscribe this socket to all topics - which means enable this socket to receive
        /// all messages regardless of what the string prefix is.
        /// This is the same as calling Subscribe with an empty-string for the topic.
        /// </summary>
        public virtual void SubscribeToAnyTopic()
        {
            Subscribe(String.Empty);
        }

        /// <summary>
        /// Remove this socket's subscription to the given topic.
        /// </summary>
        /// <param name="topic">a String denoting which the topic to stop receiving</param>
        public new virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        /// <summary>
        /// Remove this socket's subscription to the given topic.
        /// </summary>
        /// <param name="topic">a String denoting which the topic to stop receiving</param>
        /// <param name="encoding">the Encoding to use when converting the topic string internally into a byte-array</param>
        public virtual void Unsubscribe(string topic, Encoding encoding)
        {
            Unsubscribe(encoding.GetBytes(topic));
        }

        /// <summary>
        /// Remove this socket's subscription to the given topic.
        /// </summary>
        /// <param name="topic">a byte-array denoting which the topic to stop receiving</param>
        public new virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }
    }
}
