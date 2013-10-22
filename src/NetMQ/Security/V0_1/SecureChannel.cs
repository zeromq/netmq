using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;


namespace NetMQ.Security.V0_1
{
    public class SecureChannel : ISecureChannel
    {
        private HandshakeLayer m_handshakeLayer;
        private RecordLayer m_recordLayer;

        private OutgoingMessageBag m_outgoingMessageBag;

        /// <summary>
        /// This is a fixed array of 2 bytes that contain the protocol-version, { 0, 1 }.
        /// </summary>
        private readonly byte[] m_protocolVersion = new byte[] { 0, 1 };

        public SecureChannel(ConnectionEnd connectionEnd)
        {
            m_handshakeLayer = new HandshakeLayer(this, connectionEnd);
            m_handshakeLayer.CipherSuiteChange += OnCipherSuiteChangeFromHandshakeLayer;

            m_recordLayer = new RecordLayer(m_protocolVersion);

            m_outgoingMessageBag = new OutgoingMessageBag(this);
        }

        /// <summary>
        /// Get or set this boolean-flag that indicates whether a change-cipher-suite message has arrived.
        /// </summary>
        internal bool ChangeSuiteChangeArrived { get; private set; }

        /// <summary>
        /// Get whether the SecurreChannel is ready to exchange content messages.
        /// </summary>
        public bool SecureChannelReady { get; private set; }

        /// <summary>
        /// Get or set the X.509 digital certificate to be used for encryption of this channel.
        /// </summary>
        public X509Certificate2 Certificate
        {
            get { return m_handshakeLayer.LocalCertificate; }
            set { m_handshakeLayer.LocalCertificate = value; }
        }

        /// <summary>
        /// Get or set the collection of cipher-suites that are available. This maps to a simple byte-array.
        /// </summary>
        public CipherSuite[] AllowedCipherSuites
        {
            get { return m_handshakeLayer.AllowedCipherSuites; }
            set { m_handshakeLayer.AllowedCipherSuites = value; }
        }

        /// <summary>
        /// Assign the delegate to use to verify the X.509 certificate.
        /// </summary>
        /// <param name="verifyCertificate"></param>
        public void SetVerifyCertificate(VerifyCertificateDelegate verifyCertificate)
        {
            m_handshakeLayer.VerifyCertificate = verifyCertificate;
        }

        /// <summary>
        /// Process handshake and change cipher suite messages. This method should be called for every incoming message until the method returns true.
        /// You cannot encrypt or decrypt messages until the method return true.
        /// Each call to the method may include outgoing messages that need to be sent to the other peer.
        /// Note: Within this library, this method is ONLY called from within the unit-tests.
        /// </summary>
        /// <param name="incomingMessage">the incoming message from the other peer</param>
        /// <param name="outgoingMesssages">the list of outgoing messages that need to be sent to the other peer</param>
        /// <returns>true when the method completes the handshake stage and the SecureChannel is ready to encrypt and decrypt messages</returns>
        public bool ProcessMessage(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMesssages)
        {
            ContentType contentType = ContentType.Handshake;

            if (incomingMessage != null)
            {
                // Verify that the first two frames are the protocol-version and the content-type,
                NetMQFrame protocolVersionFrame = incomingMessage.Pop();
                byte[] protocolVersionBytes = protocolVersionFrame.ToByteArray();

                if (protocolVersionBytes.Length != 2)
                {
                    throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFrameLength, "Wrong length for protocol version frame");
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

                // Verify that the content-type is either handshake, or change-cipher-suit..
                contentType = (ContentType)contentTypeFrame.Buffer[0];

                if (contentType != ContentType.ChangeCipherSpec && contentType != ContentType.Handshake)
                {
                    throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidContentType, "Unknown content type");
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

                // Move the messages from the saved list over to the outgoing Messages collection..
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
                throw new NetMQSecurityException(NetMQSecurityErrorCode.SecureChannelNotReady, "Cannot encrypt messages until the secure channel is ready");
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
