using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Cryptography;

namespace NetMQ.Security
{
  public class NetMQClientSecureChannel : BaseSecureChannel
  {
    private RSAPKCS1KeyExchangeFormatter m_keyExchangeFormatter;

    public NetMQClientSecureChannel()
    {
      m_keyExchangeFormatter = new RSAPKCS1KeyExchangeFormatter();
    }

    enum State
    {
      HelloRequest,
      ClientHello,
      ServerHello,
      ServerCertificate,
      ClientFinished,
      ServerFinished
    }

    private State m_state = State.HelloRequest;

    private X509Certificate2 m_serverCertificate;


    public bool InitChannel(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      if (m_state == State.HelloRequest)
      {
        Debug.Assert(incomingMessage == null);

        OnHelloRequest(outgoingMessages);

        return false; 
      }
      else
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
          case HandshakeType.ServerHello:
            OnServerHello(incomingMessage, outgoingMessages);
            break;
          case HandshakeType.Certificate:
            OnCertificate(incomingMessage, outgoingMessages);
            break;
          case HandshakeType.ServerHelloDone:
            OnServerHelloDone(incomingMessage, outgoingMessages);
            break;
          case HandshakeType.Finished:
            OnFinished(incomingMessage, outgoingMessages);
            break;
          default:
            throw new InvalidException("Invalid state");
        }
      }

      return m_state == State.ServerFinished;
    }

    private void OnServerHello(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      WriteMessageToHandshakeStream(incomingMessage);

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }

      if (m_state != State.ClientHello)
      {
        throw new InvalidException("Wrong message type");
      }

      ServerRandomNumber = incomingMessage[1].Buffer;

      m_state = State.ServerHello;
    }

    private void OnCertificate(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      WriteMessageToHandshakeStream(incomingMessage);

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }

      if (m_state != State.ServerHello)
      {
        throw new InvalidException("Wrong message type");
      }

      m_serverCertificate = new X509Certificate2(incomingMessage[1].Buffer);

      ValidateCertificate();

      m_state = State.ServerCertificate;
    }

    private void OnServerHelloDone(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      WriteMessageToHandshakeStream(incomingMessage);

      if (incomingMessage.FrameCount != 1)
      {
        throw new InvalidException("wrong amount of frames");
      }

      if (m_state != State.ServerCertificate)
      {
        throw new InvalidException("Wrong message type");
      }

      NetMQMessage clientKetExchangeMessage = new NetMQMessage();
      clientKetExchangeMessage.Append(new byte[] { (byte)HandshakeType.ClientKetExchange });

      // create pre master secret
      byte[] preMasterSecret = GeneratePreMasterSecret();

      // encrypt the pre master secret with RSA and add a frame
      m_keyExchangeFormatter.SetKey(m_serverCertificate.PublicKey.Key);
      clientKetExchangeMessage.Append(m_keyExchangeFormatter.CreateKeyExchange(preMasterSecret));

      // add the client key exchange to the outgoign messages
      outgoingMessages.Add(clientKetExchangeMessage);

      // write the message until now before calculating the mac
      WriteMessageToHandshakeStream(clientKetExchangeMessage);

      // initliaze the keys, cipher algorithm and mac algorithm
      InitializeCipherSuite(preMasterSecret, true);

      // creat the client finished message
      NetMQMessage plainClientFinishedMessage = new NetMQMessage();
      plainClientFinishedMessage.Append(new byte[] { (byte)HandshakeType.Finished });

      // generate the verify data
      byte[] verifyData = GenerateVerifyData("client finished");
      plainClientFinishedMessage.Append(verifyData);

      // write the plain message
      WriteMessageToHandshakeStream(plainClientFinishedMessage);

      // encrypt the client finish message and add to the outgoing messages      
      NetMQMessage encryptClientFinishedMessage = EncryptMessage(plainClientFinishedMessage);      
      outgoingMessages.Add(encryptClientFinishedMessage);

      m_state = State.ClientFinished;
    }

    private void OnFinished(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMessages)
    {
      if (m_state != State.ClientFinished)
      {
        throw new InvalidException("Wrong message type");
      }

      if (incomingMessage.FrameCount != 2)
      {
        throw new InvalidException("wrong amount of frames");
      }

      // generate the verify data
      byte[] computeVerifyData = GenerateVerifyData("server finished");

      byte[] serverVerifyData = incomingMessage[1].Buffer;

      if (incomingMessage[1].BufferSize != incomingMessage[1].MessageSize)
      {
        serverVerifyData = new byte[incomingMessage[1].MessageSize];
        Buffer.BlockCopy(incomingMessage[1].Buffer, 0, serverVerifyData, 0, incomingMessage[1].MessageSize);
      }

      if (computeVerifyData.Length != serverVerifyData.Length)
      {
        throw new InvalidException("Server verify data is wrong");
      }

      for (int i = 0; i < computeVerifyData.Length; i++)
      {
        if (computeVerifyData[i] != serverVerifyData[i])
        {
          throw new InvalidException("Server verify data is wrong");
        }
      }

      WriteMessageToHandshakeStream(incomingMessage);

      m_state = State.ServerFinished;
    }

    private bool OnHelloRequest(IList<NetMQMessage> outgoingMessages)
    {      
      NetMQMessage outgoingMessage = new NetMQMessage();

      outgoingMessage.Append(new byte[] { (byte)HandshakeType.ClientHello });

      ClientRandomNumber = GenerateRandomNumber();
      outgoingMessage.Append(ClientRandomNumber);

      m_state = State.ClientHello;

      WriteMessageToHandshakeStream(outgoingMessage);

      outgoingMessages.Add(outgoingMessage);

      return false;
    }



    private void ValidateCertificate()
    {

    }
  }
}
