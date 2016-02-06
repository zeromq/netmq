using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// Class SecureChannel implements ISecureChannel and provides a secure communication channel between a client and a server.
    /// It provides for a X.509 certificate, and methods to process, encrypt, and decrypt messages.
    /// </summary>
    public class SecureChannel : ISecureChannel
    {
        private HandshakeLayer m_handshakeLayer;
        private RecordLayer m_recordLayer;

        private readonly OutgoingMessageBag m_outgoingMessageBag;

        /// <summary>
        /// This is a fixed array of 2 bytes that contain the protocol-version, { 0, 1 }.
        /// </summary>
        private readonly byte[] m_protocolVersion = new byte[] { 0, 1 };

        /// <summary>
        /// Create a new SecureChannel with the given <see cref="ConnectionEnd"/>.
        /// </summary>
        /// <param name="connectionEnd">the ConnectionEnd that this channel is to talk to</param>
        public SecureChannel(ConnectionEnd connectionEnd)
        {
            m_handshakeLayer = new HandshakeLayer(this, connectionEnd);
            m_handshakeLayer.CipherSuiteChange += OnCipherSuiteChangeFromHandshakeLayer;

            m_recordLayer = new RecordLayer(m_protocolVersion);

            m_outgoingMessageBag = new OutgoingMessageBag(this);
        }

        /// <summary>
        /// Get whether a change-cipher-suite message has arrived.
        /// </summary>
        internal bool ChangeSuiteChangeArrived { get; private set; }

        /// <summary>
        /// Get whether this SecureChannel is ready to exchange content messages.
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
        /// </summary>
        /// <param name="incomingMessage">the incoming message from the other peer</param>
        /// <param name="outgoingMesssages">the list of outgoing messages that need to be sent to the other peer</param>
        /// <returns>true when the method completes the handshake stage and the SecureChannel is ready to encrypt and decrypt messages</returns>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidContentType: Unknown content type.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidFrameLength: Wrong length for protocol version frame.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidFrameLength: Wrong length for message size.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidProtocolVersion: Wrong protocol version.</exception>
        /// <remarks>
        /// Note: Within this library, this method is ONLY called from within the unit-tests.
        /// </remarks>
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

        /// <param name="contentType">This identifies the type of content: ChangeCipherSpec, Handshake, or ApplicationData.</param>
        /// <param name="plainMessage">The unencrypted form of the message to be encrypted.</param>
        internal NetMQMessage InternalEncryptAndWrapMessage(ContentType contentType, NetMQMessage plainMessage)
        {
            NetMQMessage encryptedMessage = m_recordLayer.EncryptMessage(contentType, plainMessage);
            encryptedMessage.Push(new byte[] { (byte)contentType });
            encryptedMessage.Push(m_protocolVersion);

            return encryptedMessage;
        }

        /// <summary>
        /// Encrypt the given NetMQMessage, wrapping it's content as application-data and prefixing it with the encryption protocol.
        /// </summary>
        /// <param name="plainMessage">The unencrypted form of the message to be encrypted.</param>
        /// <returns>a new NetMQMessage that is encrypted</returns>
        /// <exception cref="ArgumentNullException">plainMessage must not be null.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.SecureChannelNotReady: The secure channel must be ready.</exception>
        public NetMQMessage EncryptApplicationMessage([NotNull] NetMQMessage plainMessage)
        {
            if (!SecureChannelReady)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.SecureChannelNotReady, "Cannot encrypt messages until the secure channel is ready");
            }

            if (plainMessage == null)
            {
                throw new ArgumentNullException("plainMessage");
            }

            return InternalEncryptAndWrapMessage(ContentType.ApplicationData, plainMessage);
        }

        /// <summary>
        /// Decrypt the given NetMQMessage, the first frame of which is assumed to contain the protocol version.
        /// </summary>
        /// <param name="cipherMessage">the NetMQMessage to decrypt</param>
        /// <returns>a NetMQMessage with the application-data decrypted</returns>
        /// <exception cref="ArgumentNullException">cipherMessage must not be null.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.SecureChannelNotReady: The secure channel must be ready.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidFramesCount: The cipher message must have at least 2 frames.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidProtocolVersion: The protocol must be the correct version.</exception>
        /// <exception cref="NetMQSecurityException">NetMQSecurityErrorCode.InvalidContentType: The message must contain application data.</exception>
        public NetMQMessage DecryptApplicationMessage([NotNull] NetMQMessage cipherMessage)
        {
            if (!SecureChannelReady)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.SecureChannelNotReady, "Cannot decrypt messages until the secure channel is ready");
            }

            if (cipherMessage == null)
            {
                throw new ArgumentNullException("cipherMessage");
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
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidContentType, "Not an application data message");
            }

            return m_recordLayer.DecryptMessage(ContentType.ApplicationData, cipherMessage);
        }

        /// <summary>
        /// Release any contained resources of this SecureChannel object.
        /// </summary>
        /// <remarks>
        /// This disposes of the handshake-layer and the record-layer.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release any contained resources of this SecureChannel object.
        /// </summary>
        /// <param name="disposing">set this to true if disposing of managed resources</param>
        /// <remarks>
        /// This disposes of the handshake-layer and the record-layer.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

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
