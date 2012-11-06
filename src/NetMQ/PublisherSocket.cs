using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class PublisherSocket : BaseSocket
    {
        public class PublisherSendMessage
        {
            PublisherSocket m_publisherSocket;

            public PublisherSendMessage(PublisherSocket publisherSocket)
            {
                m_publisherSocket = publisherSocket;
            }

            public void Send(byte[] data)
            {
                m_publisherSocket.SendInternal(data, data.Length, false, false);
            }

            public void Send(byte[] data, int length)
            {
                m_publisherSocket.SendInternal(data, length, false, false);
            }

            public void Send(byte[] data, bool dontWait)
            {
                m_publisherSocket.SendInternal(data, data.Length, dontWait, false);
            }

            public void Send(byte[] data, int length, bool dontWait)
            {
                m_publisherSocket.SendInternal(data, length, dontWait, false);
            }

            public void Send(string message)
            {
                m_publisherSocket.SendInternal(message, false, false);
            }

            public void Send(string message, bool dontWait)
            {
                m_publisherSocket.SendInternal(message, dontWait, false);
            }

            public PublisherSendMessage SendMore(byte[] data)
            {
                m_publisherSocket.SendInternal(data, data.Length, false, true);
                return this;
            }

            public PublisherSendMessage SendMore(byte[] data, int length)
            {
                m_publisherSocket.SendInternal(data, length, false, true);
                return this;
            }

            public PublisherSendMessage SendMore(byte[] data, bool dontWait)
            {
                m_publisherSocket.SendInternal(data, data.Length, dontWait, true);
                return this;
            }

            public PublisherSendMessage SendMore(byte[] data, int length, bool dontWait)
            {
                m_publisherSocket.SendInternal(data, length, dontWait, true);
                return this;
            }

            public PublisherSendMessage SendMore(string message)
            {
                m_publisherSocket.SendInternal(message, false, true);
                return this;
            }

            public PublisherSendMessage SendMore(string message, bool dontWait)
            {
                m_publisherSocket.SendInternal(message, dontWait, true);
                return this;
            }
        }

        public PublisherSocket(SocketBase socketHandle)
            : base(socketHandle)
        {
        }

        public PublisherSendMessage SendTopic(byte[] data)
        {
            SendInternal(data, data.Length, false, true);
            return new PublisherSendMessage(this);
        }

        public PublisherSendMessage SendTopic(byte[] data, int length)
        {
            SendInternal(data, length, false, true);
            return new PublisherSendMessage(this);
        }

        public PublisherSendMessage SendTopic(byte[] data, bool dontWait)
        {
            SendInternal(data, data.Length, dontWait, true);
            return new PublisherSendMessage(this);
        }

        public PublisherSendMessage SendTopic(byte[] data, int length, bool dontWait)
        {
            SendInternal(data, length, dontWait, true);
            return new PublisherSendMessage(this);
        }

        public PublisherSendMessage SendTopic(string message)
        {
            SendInternal(message, false, true);
            return new PublisherSendMessage(this);
        }

        public PublisherSendMessage SendTopic(string message, bool dontWait)
        {
            SendInternal(message, dontWait, true);
            return new PublisherSendMessage(this);
        }

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
    }
}
