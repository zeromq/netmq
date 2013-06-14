using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1
{
  class OutgoingMessageBag 
  {
    private readonly SecureChannel m_secureChannel;
    private IList<NetMQMessage> m_messages;

    public OutgoingMessageBag(SecureChannel secureChannel)
    {
      m_secureChannel = secureChannel;
      m_messages = new List<NetMQMessage>();
    }

    public IEnumerable<NetMQMessage> Messages
    {
      get { return m_messages; }      
    }

    public void AddCipherChangeMessage(NetMQMessage message)
    {
      m_messages.Add(m_secureChannel.InternalEncryptAndWrapMessage(ContentType.ChangeCipherSpec, message));
    }

    public void AddHandshakeMessage(NetMQMessage message)
    {
      m_messages.Add(m_secureChannel.InternalEncryptAndWrapMessage(ContentType.Handshake, message));
    }

    public void Clear()
    {
      m_messages.Clear();
    }
  }
}
