using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NUnit.Framework;
using NetMQ.Security;
using NetMQ.Security.V0_1;

namespace NetMQ.Tests
{
  [TestFixture]
  class SecurityTests
  {
    private void DoHandshake(out SecureChannel clientSecureChannel,
      out SecureChannel serverSecureChannel)
    {
      X509Certificate2 certificate = new X509Certificate2("NetMQ.Testing.pfx", "1");

      serverSecureChannel = new SecureChannel(ConnectionEnd.Server);
      serverSecureChannel.Certificate = certificate;

      clientSecureChannel = new SecureChannel(ConnectionEnd.Client);
      
      IList<NetMQMessage> clientOutgoingMessages = new List<NetMQMessage>();
      IList<NetMQMessage> serverOutgoingMessages = new List<NetMQMessage>();
      
      bool serverComplete = false;

      bool clientComplete = clientSecureChannel.ProcessMessage(null, clientOutgoingMessages);

      while (!serverComplete || !clientComplete)
      {
        if (!serverComplete)
        {
          foreach (var message in clientOutgoingMessages)
          {
            serverComplete = serverSecureChannel.ProcessMessage(message, serverOutgoingMessages);

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
            clientComplete = clientSecureChannel.ProcessMessage(message, clientOutgoingMessages);

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
      SecureChannel clientSecureChannel;
      SecureChannel serverSecureChannel;

      DoHandshake(out clientSecureChannel, out serverSecureChannel);

      string text = "hellohello-hello1";      

      NetMQMessage message = new NetMQMessage();
      message.Append(text);

      NetMQMessage encryptedMessage = clientSecureChannel.EncryptApplicationMessage(message);

      NetMQMessage dycryptedMessage = serverSecureChannel.DecryptApplicationMessage(encryptedMessage);

      string result = dycryptedMessage[0].ConvertToString();

      Assert.AreEqual(text, result);
    }

    [Test]
    public void Handshake()
    {
      
      
    }

  }
}
