using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetMQ.Security.V0_1
{
  public class SecureChannel : ISecureChannel
  {    
    private HandshakeLayer m_handshakeLayer;
    private RecordLayer m_recordLayer;

    private OutgoingMessageBag m_outgoingMessageBag;
    
    private readonly byte[] m_protocolVersion = new byte[] { 0, 1 };        

    public SecureChannel(ConnectionEnd connectionEnd)
    {
      m_handshakeLayer = new HandshakeLayer(this, connectionEnd);
      m_handshakeLayer.CipherSuiteChange += OnCipherSuiteChangeFromHandshakeLayer;

      m_recordLayer = new RecordLayer(m_protocolVersion);

      m_outgoingMessageBag = new OutgoingMessageBag(this);      
    }

    internal bool ChangeSuiteChangeArrived { get; private set; }

    public bool SecureChannelReady { get; private set; }

  	public X509Certificate2 Certificate
    {
      get { return m_handshakeLayer.LocalCertificate; }
      set { m_handshakeLayer.LocalCertificate = value; }
    }

    public CipherSuite[] AllowedCipherSuites
    {
      get { return m_handshakeLayer.AllowedCipherSuites; }
      set { m_handshakeLayer.AllowedCipherSuites = value; }
    }

    public void SetVerifyCertificate(VerifyCertificateDelegate verifyCertificate)
    {      
      m_handshakeLayer.VerifyCertificate = verifyCertificate; 
    }

    public bool ProcessMessage(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMesssages)
    {      
      ContentType contentType = ContentType.Handshake;

      if (incomingMessage != null)
      {
        NetMQFrame protocolVersionFrame = incomingMessage.Pop();
        byte[] protocolVersionBytes = protocolVersionFrame.ToByteArray();

        if (protocolVersionBytes.Length != 2)
        {
          throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFrameLength,  "Wrong length for protocol version frame");
        }

        if (!protocolVersionBytes.SequenceEqual(m_protocolVersion))
        {
          throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidProtocolVersion, "Wrong protocol version");
        }

        NetMQFrame contentTypeFrame = incomingMessage.Pop();

        if (contentTypeFrame.MessageSize != 1)
        {
          throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFrameLength, "wrong length for message size");
        }

        contentType = (ContentType) contentTypeFrame.Buffer[0];

        if (contentType != ContentType.ChangeCipherSpec && contentType != ContentType.Handshake)
        {
          throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidContentType, "Unkown content type");
        }
        
        if (ChangeSuiteChangeArrived)
        {
          incomingMessage = m_recordLayer.DecryptMessage(contentType, incomingMessage);
        }        
      }

      bool result = false;

      if (contentType == ContentType.Handshake)
      {
        result = m_handshakeLayer.ProcessMessages(incomingMessage, m_outgoingMessageBag);

        foreach (NetMQMessage outgoingMesssage in m_outgoingMessageBag.Messages)
        {
          outgoingMesssages.Add(outgoingMesssage);
        }

        m_outgoingMessageBag.Clear();
      }
      else
      {
        ChangeSuiteChangeArrived = true;
      }

      return (SecureChannelReady = result && ChangeSuiteChangeArrived);
    }

    private void OnCipherSuiteChangeFromHandshakeLayer(object sender, EventArgs e)
    {
      NetMQMessage changeCipherMessage = new NetMQMessage();
      changeCipherMessage.Append(new byte[] { 1 });

      m_outgoingMessageBag.AddCipherChangeMessage(changeCipherMessage);

      m_recordLayer.SecurityParameters = m_handshakeLayer.SecurityParameters;

      m_recordLayer.InitalizeCipherSuite();
    }
    
    internal NetMQMessage InternalEncryptAndWrapMessage(ContentType contentType, NetMQMessage plainMessage)
    {
      NetMQMessage encryptedMessage = m_recordLayer.EncryptMessage(contentType, plainMessage);
      encryptedMessage.Push(new byte[] { (byte)contentType });
      encryptedMessage.Push(m_protocolVersion);

      return encryptedMessage;
    }    

    public NetMQMessage EncryptApplicationMessage(NetMQMessage plainMessage)
    {
      if (!SecureChannelReady)
      {
        throw new NetMQSecurityException(NetMQSecurityErrorCode.SecureChannelNotReady,  "Cannot encrypt messages until the secure channel is ready");
      }

			if (plainMessage == null)
			{
				throw new ArgumentNullException("plainMessage is null");
			}			

      return InternalEncryptAndWrapMessage(ContentType.ApplicationData, plainMessage);
    }

    public NetMQMessage DecryptApplicationMessage(NetMQMessage cipherMessage)
    {
      if (!SecureChannelReady)
      {
        throw new NetMQSecurityException(NetMQSecurityErrorCode.SecureChannelNotReady, "Cannot decrypt messages until the secure channel is ready");
      }

			if (cipherMessage == null)
			{
				throw new ArgumentNullException("cipherMessage is null");
			}

			if (cipherMessage.FrameCount < 2)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "cipher message should have at least 2 frames");
			}

      NetMQFrame protocolVersionFrame = cipherMessage.Pop();
      NetMQFrame contentTypeFrame = cipherMessage.Pop();

      if (!protocolVersionFrame.ToByteArray().SequenceEqual(m_protocolVersion))
      {
        throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidProtocolVersion, "Wrong protocol version");
      }

      ContentType contentType = (ContentType)contentTypeFrame.Buffer[0];

      if (contentType != ContentType.ApplicationData)
      {
        throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidContentType, "Not an applicagtion data message");
      }

      return m_recordLayer.DecryptMessage(ContentType.ApplicationData, cipherMessage);
    }

    public void Dispose()
    {
			if (m_handshakeLayer != null)
			{
				m_handshakeLayer.Dispose();
				m_handshakeLayer = null;
			}

			if (m_recordLayer != null)
			{
				m_recordLayer.Dispose();
				m_recordLayer = null;
			}
    }
  }
}
