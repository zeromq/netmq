using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
      clientSecureChannel.SetVerifyCertificate(c => true);

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
      using (NetMQContext context = NetMQContext.Create())
      {
        Task t = Task.Factory.StartNew(() => Server(context));

        Thread.Sleep(1000);

        Client(context);

        t.Wait();
      }
    }

    private void Client(NetMQContext context)
    {
      using (var socket = context.CreateDealerSocket())
      {
        socket.Connect("tcp://127.0.0.1:5556");

        SecureChannel secureChannel = new SecureChannel(ConnectionEnd.Client);

        // we are not using signed certificate so we need to validate the certificate of the server
        // by default the secure channel is checking that the source of the certitiface is root certificate authority
        secureChannel.SetVerifyCertificate(c => true);

        IList<NetMQMessage> outgoingMessages = new List<NetMQMessage>();

        // call the process message with null as the incoming message 
        // because the client is initiating the connection
        secureChannel.ProcessMessage(null, outgoingMessages);

        // the process message method fill the outgoing messages list with 
        // messages to send over the socket
        foreach (NetMQMessage outgoingMessage in outgoingMessages)
        {
          socket.SendMessage(outgoingMessage);
        }
        outgoingMessages.Clear();

        // waiting for a message from the server
        NetMQMessage incomingMessage = socket.ReceiveMessage();

        // calling ProcessMessage until ProcessMessage return true and the SecureChannel is ready
        // to encrypt and decrypt messages
        while (!secureChannel.ProcessMessage(incomingMessage, outgoingMessages))
        {
          foreach (NetMQMessage outgoingMessage in outgoingMessages)
          {
            socket.SendMessage(outgoingMessage);
          }
          outgoingMessages.Clear();

          incomingMessage = socket.ReceiveMessage();
        }

        foreach (NetMQMessage outgoingMessage in outgoingMessages)
        {
          socket.SendMessage(outgoingMessage);
        }
        outgoingMessages.Clear();


        // you can now use the secure channel to encrypt messages
        NetMQMessage plainMessage = new NetMQMessage();
        plainMessage.Append("Hello");

        // encrypting the message and sending it over the socket
        socket.SendMessage(secureChannel.EncryptApplicationMessage(plainMessage));
      }
    }

    private void Server(NetMQContext context)
    {
      // we are using dealer here, but we can use router as well, we just have to manager
      // SecureChannel for each identity
      using (var socket = context.CreateDealerSocket())
      {
        socket.Bind("tcp://*:5556");

        SecureChannel secureChannel = new SecureChannel(ConnectionEnd.Server);

        // we need to set X509Certificate with a private key for the server
        X509Certificate2 certificate = new X509Certificate2("NetMQ.Testing.pfx", "1");
        secureChannel.Certificate = certificate;

        IList<NetMQMessage> outgoingMessages = new List<NetMQMessage>();

        // waiting for message from client
        NetMQMessage incomingMessage = socket.ReceiveMessage();

        // calling ProcessMessage until ProcessMessage return true and the SecureChannel is ready
        // to encrypt and decrypt messages
        while (!secureChannel.ProcessMessage(incomingMessage, outgoingMessages))
        {
          foreach (NetMQMessage outgoingMessage in outgoingMessages)
          {
            socket.SendMessage(outgoingMessage);
          }
          outgoingMessages.Clear();

          incomingMessage = socket.ReceiveMessage();
        }
        foreach (NetMQMessage outgoingMessage in outgoingMessages)
        {
          socket.SendMessage(outgoingMessage);
        }
        outgoingMessages.Clear();

        // this message is now encrypted
        NetMQMessage cipherMessage = socket.ReceiveMessage();

        // decrypting the message
        NetMQMessage plainMessage = secureChannel.DecryptApplicationMessage(cipherMessage);
        Console.WriteLine(plainMessage.First.ConvertToString());
      }
    }

  }
}
