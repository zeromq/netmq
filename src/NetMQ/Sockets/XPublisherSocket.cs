using System.Text;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
    public class XPublisherSocket : NetMQSocket
    {
        internal XPublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">Topic to subscribe</param>
        public new virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">Topic to subscribe</param>
        /// <param name="encoding"></param>
        public virtual void Subscribe(string topic, Encoding encoding)
        {
            Subscribe(encoding.GetBytes(topic));
        }

        /// <summary>
        /// In case of socket set to manual mode will subscribe the last subscriber to the topic
        /// </summary>
        /// <param name="topic">Topic to subscribe</param>
        public new virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">Topic to unsubscribe from</param>
        public new virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">Topic to unsubscribe from</param>
        /// <param name="encoding"></param>
        public virtual void Unsubscribe(string topic, Encoding encoding)
        {
            Unsubscribe(encoding.GetBytes(topic));
        }

        /// <summary>
        /// In case of socket set to manual mode will unsubscribe the last subscriber from a topic
        /// </summary>
        /// <param name="topic">Topic to unsubscribe from</param>
        public new virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        public void ClearWelcomeMessage()
        {
            SetSocketOption(ZmqSocketOptions.XPublisherWelcomeMessage, null);
        }

        public void SetWelcomeMessage(string welcomeMessage, Encoding encoding)
        {
            SetWelcomeMessage(encoding.GetBytes(welcomeMessage));
        }

        public void SetWelcomeMessage(string welcomeMessage)
        {
            SetWelcomeMessage(Encoding.ASCII.GetBytes(welcomeMessage));
        }

        public void SetWelcomeMessage(byte[] welcomeMessage)
        {
            SetSocketOption(ZmqSocketOptions.XPublisherWelcomeMessage, welcomeMessage);
        }
    }
}
