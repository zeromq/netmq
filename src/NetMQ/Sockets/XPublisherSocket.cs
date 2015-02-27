using System.Text;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    /// <summary>
    /// An XPublisherSocket is a NetMQSocket intended to be used as the XPub in the XPub/XSub pattern.
    /// The intended usage is for serving, together with a matching XSubscriberSocket,
    /// as a stable intermediary between a PublisherSocket and it's SubscriberSockets.
    /// </summary>
    public class XPublisherSocket : NetMQSocket
    {
        internal XPublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">a string specifying the Topic to subscribe to</param>
        public new virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">a string specifying the Topic to subscribe to</param>
        /// <param name="encoding">the character-Encoding to use when converting the topic string internally into a byte-array</param>
        public virtual void Subscribe(string topic, Encoding encoding)
        {
            Subscribe(encoding.GetBytes(topic));
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">a byte-array specifying the Topic to subscribe to</param>
        public new virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">a string specifying the Topic to unsubscribe from</param>
        public new virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">a string specifying the Topic to unsubscribe from</param>
        /// <param name="encoding">the character-Encoding to use when converting the topic string internally into a byte-array</param>
        public virtual void Unsubscribe(string topic, Encoding encoding)
        {
            Unsubscribe(encoding.GetBytes(topic));
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">a byte-array specifying the Topic to unsubscribe from</param>
        public new virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        /// <summary>
        /// Publisher sockets generally send a welcome-message to subscribers to give an indication that they have successful subscribed.
        /// This method clears that message, such that none is sent.
        /// </summary>
        public void ClearWelcomeMessage()
        {
            SetSocketOption(ZmqSocketOptions.XPublisherWelcomeMessage, null);
        }

        /// <summary>
        /// Publisher sockets send a welcome-message to subscribers to give an indication that they have successful subscribed.
        /// This method is how you set the text of that welcome-message.
        /// </summary>
        /// <param name="welcomeMessage">a string denoting the new value for the welcome-message</param>
        /// <param name="encoding">the character-Encoding to use when converting the topic string internally into a byte-array</param>
        public void SetWelcomeMessage(string welcomeMessage, Encoding encoding)
        {
            SetWelcomeMessage(encoding.GetBytes(welcomeMessage));
        }

        /// <summary>
        /// Publisher sockets send a welcome-message to subscribers to give an indication that they have successful subscribed.
        /// This method is how you set the text of that welcome-message. The Encoding is assumed to be ASCII.
        /// </summary>
        /// <param name="welcomeMessage">a string denoting the new value for the welcome-message</param>
        public void SetWelcomeMessage(string welcomeMessage)
        {
            SetWelcomeMessage(Encoding.ASCII.GetBytes(welcomeMessage));
        }

        /// <summary>
        /// Publisher sockets send a welcome-message to subscribers to give an indication that they have successful subscribed.
        /// This method is how you set the text of that welcome-message. The Encoding is assumed to be ASCII.
        /// </summary>
        /// <param name="welcomeMessage">a byte-array denoting the new value for the welcome-message</param>
        public void SetWelcomeMessage(byte[] welcomeMessage)
        {
            SetSocketOption(ZmqSocketOptions.XPublisherWelcomeMessage, welcomeMessage);
        }
    }
}
