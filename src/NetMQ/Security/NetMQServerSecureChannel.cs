using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetMQ.Security
{
  public class NetMQServerSecureChannel : BaseSecureChannel
  {
    enum State
    {
      HelloRequest,
      ServerHelloDone,
      ClientKeyExchange,
      ClientFinished,
      ServerFinished
    }

    private State m_state = State.HelloRequest;
    private readonly X509Certificate2 m_serverCertificate;

    private RSAPKCS1KeyExchangeDeformatter m_keyExchangeDeformatter;

    public NetMQServerSecureChannel(X509Certificate2 serverCertificate)
    {
      m_serverCertificate = serverCertificate;
      m_keyExchangeDeformatter = new RSAPKCS1KeyExchangeDeformatter(m_serverCertificate.PrivateKey);
    }

    public bool AcceptChannel(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      if (incomingMessage == null || incomingMessage.FrameCount == 0)
      {
        throw new ArgumentNullException();
      }

      if (CipherSuiteInitialized)
      {
        incomingMessage = DecryptMessage(incomingMessage);
      }

      if (incomingMessage[0].MessageSize != 1)
      {
        throw new InvalidException("First frame should be the handshake type");
      }

      HandshakeType incomingHandshakeType = (HandshakeType)incomingMessage[0].Buffer[0];
      
      switch (incomingHandshakeType)
      {
        case HandshakeType.ClientHello:
          OnClientHello(incomingMessage, outgoingMessages);
          break;
        case HandshakeType.ClientKetExchange:
          OnClientKeyExchange(incomingMessage, outgoingMessages);
          break;
          case HandshakeType.Finished:
          OnFinished(incomingMessage, outgoingMessages);
          break;
        default:
          throw new InvalidException("Invalid state");
      }

      return m_state == State.ServerFinished;
    }

    private void OnFinished(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      if (m_state != State.ClientKeyExchange)
      {
        throw new InvalidException("Wrong message type");
      }

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }

      // generate the verify data
      byte[] computeVerifyData = GenerateVerifyData("client finished");

      byte[] clientVerifyData = incomingMessage[1].Buffer;

      if (incomingMessage[1].BufferSize != incomingMessage[1].MessageSize)
      {
        clientVerifyData = new byte[incomingMessage[1].MessageSize];
        Buffer.BlockCopy(incomingMessage[1].Buffer, 0, clientVerifyData, 0, incomingMessage[1].MessageSize);
      }

      if (computeVerifyData.Length != clientVerifyData.Length)
      {
        throw new InvalidException("client verify data is wrong");
      }

      for (int i = 0; i < computeVerifyData.Length; i++)
      {
        if (computeVerifyData[i] != clientVerifyData[i])
        {
          throw new InvalidException("client verify data is wrong");
        }
      }

      WriteMessageToHandshakeStream(incomingMessage);

      // creat the client finished message
      NetMQMessage plainServerFinishedMessage = new NetMQMessage();
      plainServerFinishedMessage.Append(new byte[] { (byte)HandshakeType.Finished });

      // generate the verify data
      byte[] verifyData = GenerateVerifyData("server finished");
      plainServerFinishedMessage.Append(verifyData);

      // encrypt the client finish message and add to the outgoing messages      
      NetMQMessage encryptServerFinishedMessage = EncryptMessage(plainServerFinishedMessage);
      outgoingMessages.Add(encryptServerFinishedMessage);

      WriteMessageToHandshakeStream(plainServerFinishedMessage);

      m_state = State.ServerFinished;
    }

    private void OnClientKeyExchange(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      WriteMessageToHandshakeStream(incomingMessage);

      if (m_state != State.ServerHelloDone)
      {
        throw new InvalidException("Wrong message type");
      }

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }

      byte[] preMasterSecret = m_keyExchangeDeformatter.DecryptKeyExchange(incomingMessage[1].Buffer);

      InitializeCipherSuite(preMasterSecret, false);

      m_state = State.ClientKeyExchange;
    }

    private void OnClientHello(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      WriteMessageToHandshakeStream(incomingMessage);

      if (m_state != State.HelloRequest)
      {
        throw new InvalidException("Wrong message type");
      }

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }
      
      ClientRandomNumber = incomingMessage[1].Buffer;

      ServerRandomNumber = GenerateRandomNumber();

      NetMQMessage serverHelloMessage = new NetMQMessage();
      serverHelloMessage.Append(new byte[] { (byte)HandshakeType.ServerHello });
      serverHelloMessage.Append(ServerRandomNumber);
      outgoingMessages.Add(serverHelloMessage);
      WriteMessageToHandshakeStream(serverHelloMessage);

      NetMQMessage certificateMessage = new NetMQMessage();
      certificateMessage.Append(new byte[] { (byte)HandshakeType.Certificate });
      certificateMessage.Append(m_serverCertificate.Export(X509ContentType.Cert));
      outgoingMessages.Add(certificateMessage);
      WriteMessageToHandshakeStream(certificateMessage);

      NetMQMessage serverHelloDoneMessage = new NetMQMessage();
      serverHelloDoneMessage.Append(new byte[] { (byte)HandshakeType.ServerHelloDone });
      outgoingMessages.Add(serverHelloDoneMessage);
      WriteMessageToHandshakeStream(serverHelloDoneMessage);

      m_state = State.ServerHelloDone;
    }
  }
}
