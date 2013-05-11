using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NUnit.Framework;
using NetMQ.Security;

namespace NetMQ.Tests
{
  [TestFixture]
  class SecurityTests
  {
    private void DoHandshake(out NetMQClientSecureChannel clientSecureChannel, out NetMQServerSecureChannel serverSecureChannel)
    {
      X509Certificate2 certificate = new X509Certificate2("NetMQ.Testing.pfx", "1");
      
      serverSecureChannel = new NetMQServerSecureChannel(certificate);
      clientSecureChannel = new NetMQClientSecureChannel();
      
      IList<NetMQMessage> clientOutgoingMessages = new List<NetMQMessage>();
      IList<NetMQMessage> serverOutgoingMessages = new List<NetMQMessage>();
      
      bool serverComplete = false;

      bool clientComplete = clientSecureChannel.InitChannel(null, clientOutgoingMessages);

      while (!serverComplete || !clientComplete)
      {
        if (!serverComplete)
        {
          foreach (var message in clientOutgoingMessages)
          {
            serverComplete = serverSecureChannel.AcceptChannel(message, serverOutgoingMessages);

            if (serverComplete)
            {
              break;
            }
          }

          clientOutgoingMessages.Clear();
        }

        if (!clientComplete)
        {
          foreach (var message in serverOutgoingMessages)
          {
            clientComplete = clientSecureChannel.InitChannel(message, clientOutgoingMessages);

            if (clientComplete)
            {
              break;
            }
          }

          serverOutgoingMessages.Clear();
        }        
      }
    }

    [Test]
    public void Encrypt()
    {
      NetMQClientSecureChannel clientSecureChannel;
      NetMQServerSecureChannel serverSecureChannel;

      DoHandshake(out clientSecureChannel, out serverSecureChannel);

      string text = "hellohello-hello1";      

      NetMQMessage message = new NetMQMessage();
      message.Append(text);

      NetMQMessage encryptedMessage = clientSecureChannel.EncryptMessage(message);

      NetMQMessage dycryptedMessage = serverSecureChannel.DecryptMessage(encryptedMessage);

      string result = dycryptedMessage[0].ConvertToString();

      Assert.AreEqual(text, result);
    }

    [Test]
    public void Handshake()
    {
      
      
    }

  }
}
