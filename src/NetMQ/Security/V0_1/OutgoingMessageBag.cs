using System.Collections.Generic;

namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// This class contains a list of NetMQMessages,
    /// and a SecureChannel to use when adding protocol messages to it.
    /// </summary>
    internal class OutgoingMessageBag
    {
        private readonly SecureChannel m_secureChannel;
        private readonly IList<NetMQMessage> m_messages;

        /// <summary>
        /// Create a new instance of an OutgoingMessageBag that will use the given SecureChannel.
        /// </summary>
        /// <param name="secureChannel">a SecureChannel object that will serve to encrypt the protocol messages</param>
        public OutgoingMessageBag(SecureChannel secureChannel)
        {
            m_secureChannel = secureChannel;
            m_messages = new List<NetMQMessage>();
        }

        /// <summary>
        /// Get the list of NetMQMessages that this OutgoingMessageBag is holding.
        /// </summary>
        public IEnumerable<NetMQMessage> Messages => m_messages;

        /// <summary>
        /// Add the given NetMQMessage to the list that this object holds, using the SecureChannel to
        /// encrypt and wrap it as a ChangeCipherSpec type of content.
        /// </summary>
        /// <param name="message">the NetMQMessage to add to the list that this object is holding</param>
        public void AddCipherChangeMessage(NetMQMessage message)
        {
            m_messages.Add(m_secureChannel.InternalEncryptAndWrapMessage(ContentType.ChangeCipherSpec, message));
        }

        /// <summary>
        /// Add the given NetMQMessage to the list that this object holds, using the SecureChannel to
        /// encrypt and wrap it as a Handshake type of content.
        /// </summary>
        /// <param name="message">the NetMQMessage to add to the list that this object is holding</param>
        public void AddHandshakeMessage(NetMQMessage message)
        {
            m_messages.Add(m_secureChannel.InternalEncryptAndWrapMessage(ContentType.Handshake, message));
        }

        /// <summary>
        /// Empty the list of NetMQMessages that this object holds.
        /// </summary>
        public void Clear()
        {
            m_messages.Clear();
        }
    }
}
