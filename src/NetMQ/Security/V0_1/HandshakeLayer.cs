// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetMQ.Security.V0_1.HandshakeMessages;
#if PRE_4
using NetMQ.zmq;
#endif


namespace NetMQ.Security.V0_1
{
    class HandshakeLayer : IDisposable
    {
        private readonly SecureChannel m_secureChannel;
        public const int RandomNumberLength = 32;
        public const int MasterSecretLength = 48;

        public string MasterSecretLabel = "master secret";

        public string ClientFinshedLabel = "client finished";
        public string ServerFinishedLabel = "server finished";

        private HandshakeType m_lastReceivedMessage = HandshakeType.HelloRequest;
        private HandshakeType m_lastSentMessage = HandshakeType.HelloRequest;

        private SHA256 m_localHash;
        private SHA256 m_remoteHash;

        private RandomNumberGenerator m_rng = new RNGCryptoServiceProvider();

        private bool m_done;

        private IPRF m_prf = new SHA256PRF();

        #region constructor
        /// <summary>
        /// Create a new HandshakeLayer object given a SecureChannel and which end of the connection it is to be.
        /// </summary>
        /// <param name="secureChannel">the SecureChannel that comprises the secure functionality of this layer</param>
        /// <param name="connectionEnd">this specifies which end of the connection - Server or Client</param>
        public HandshakeLayer(SecureChannel secureChannel, ConnectionEnd connectionEnd)
        {
            // SHA256 is a class that computes the SHA-256 (SHA stands for Standard Hasing Algorithm) of it's input.
            m_localHash = SHA256.Create();
            m_remoteHash = SHA256.Create();

            m_secureChannel = secureChannel;
            SecurityParameters = new SecurityParameters();
            SecurityParameters.Entity = connectionEnd;
            SecurityParameters.CompressionAlgorithm = CompressionMethod.Null;
            SecurityParameters.PRFAlgorithm = PRFAlgorithm.SHA256;
            SecurityParameters.CipherType = CipherType.Block;

            AllowedCipherSuites = new CipherSuite[]
        {
          CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256, 
          CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA, 
          CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256, 
          CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA, 
        };

            VerifyCertificate = c => c.Verify();
        }
        #endregion

        public SecurityParameters SecurityParameters { get; private set; }

        public CipherSuite[] AllowedCipherSuites { get; set; }

        public X509Certificate2 LocalCertificate { get; set; }

        public X509Certificate2 RemoteCertificate { get; set; }

        /// <summary>
        /// Get the Pseudo-Random number generating-Function (PRF) that is being used.
        /// </summary>
        public IPRF PRF
        {
            get { return m_prf; }
        }

        /// <summary>
        /// This event signals a change to the cipher-suite.
        /// </summary>
        public event EventHandler CipherSuiteChange;

        /// <summary>
        /// Get or set the delegate to use to call the method for verifying the certificate.
        /// </summary>
        public VerifyCertificateDelegate VerifyCertificate { get; set; }

        public bool ProcessMessages(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (incomingMessage == null)
            {
                if (m_lastReceivedMessage == m_lastSentMessage &&
                            m_lastSentMessage == HandshakeType.HelloRequest && SecurityParameters.Entity == ConnectionEnd.Client)
                {
                    OnHelloRequest(outgoingMessages);
                    return false;
                }
                else
                {
                    throw new ArgumentNullException("handshakeMessage is null");
                }
            }

            HandshakeType handshakeType = (HandshakeType)incomingMessage[0].Buffer[0];

            switch (handshakeType)
            {
                case HandshakeType.ClientHello:
                    OnClientHello(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.ServerHello:
                    OnServerHello(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.Certificate:
                    OnCertificate(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.ServerHelloDone:
                    OnServerHelloDone(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.ClientKeyExchange:
                    OnClientKeyExchange(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.Finished:
                    OnFinished(incomingMessage, outgoingMessages);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            m_lastReceivedMessage = handshakeType;

            return m_done;
        }

        /// <summary>
        /// Compute the hash of the given message twice, first using the local hashing algorithm
        /// and then again using the remote-peer hashing algorithm.
        /// </summary>
        /// <param name="message">the NetMQMessage whose frames are to be hashed</param>
        private void HashLocalAndRemote(NetMQMessage message)
        {
            HashLocal(message);
            HashRemote(message);
        }

        /// <summary>
        /// Use the local (as opposed to that of the remote-peer) hashing algorithm to compute a hash
        /// of the frames within the given NetMQMessage.
        /// </summary>
        /// <param name="message">the NetMQMessage whose frames are to be hashed</param>
        private void HashLocal(NetMQMessage message)
        {
            Hash(m_localHash, message);
        }

        /// <summary>
        /// Use the remote-peer hashing algorithm to compute a hash
        /// of the frames within the given NetMQMessage.
        /// </summary>
        /// <param name="message">the NetMQMessage whose frames are to be hashed</param>
        private void HashRemote(NetMQMessage message)
        {
            Hash(m_remoteHash, message);
        }

        /// <summary>
        /// Compute a hash of the bytes of the buffer within the frames of the given NetMQMessage.
        /// </summary>
        /// <param name="hasher">the hashing-algorithm to employ</param>
        /// <param name="message">the NetMQMessage whose frames are to be hashed</param>
        private void Hash(HashAlgorithm hash, NetMQMessage message)
        {
            foreach (NetMQFrame frame in message)
            {
                // Access the byte-array that is the frame's buffer.
                //TODO: This seems to me to be in error - it should be called with false. This is hashing a copy of the buffer, and then doing nothing with it. james hurst
                byte[] bytes = frame.ToByteArray(true);
                // Compute the hash value for the region of the input byte-array (bytes), starting at index 0,
                // and copy the resulting hash value back into the same byte-array.
                hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }
        }

        private void OnHelloRequest(OutgoingMessageBag outgoingMessages)
        {
            ClientHelloMessage clientHelloMessage = new ClientHelloMessage();

            clientHelloMessage.RandomNumber = new byte[RandomNumberLength];
            m_rng.GetBytes(clientHelloMessage.RandomNumber);

            SecurityParameters.ClientRandom = clientHelloMessage.RandomNumber;

            clientHelloMessage.CipherSuites = AllowedCipherSuites;

            NetMQMessage outgoingMessage = clientHelloMessage.ToNetMQMessage();

            HashLocalAndRemote(outgoingMessage);

            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ClientHello;
        }

        private void OnClientHello(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.HelloRequest || m_lastSentMessage != HandshakeType.HelloRequest)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Client Hello received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            ClientHelloMessage clientHelloMessage = new ClientHelloMessage();
            clientHelloMessage.SetFromNetMQMessage(incomingMessage);

            SecurityParameters.ClientRandom = clientHelloMessage.RandomNumber;

            AddServerHelloMessage(outgoingMessages, clientHelloMessage.CipherSuites);

            AddCertificateMessage(outgoingMessages);

            AddServerHelloDone(outgoingMessages);
        }

        private void AddServerHelloDone(OutgoingMessageBag outgoingMessages)
        {
            ServerHelloDoneMessage serverHelloDoneMessage = new ServerHelloDoneMessage();
            NetMQMessage outgoingMessage = serverHelloDoneMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ServerHelloDone;
        }

        private void AddCertificateMessage(OutgoingMessageBag outgoingMessages)
        {
            CertificateMessage certificateMessage = new CertificateMessage();
            certificateMessage.Certificate = LocalCertificate;

            NetMQMessage outgoingMessage = certificateMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.Certificate;
        }

        private void AddServerHelloMessage(OutgoingMessageBag outgoingMessages, CipherSuite[] cipherSuites)
        {
            ServerHelloMessage serverHelloMessage = new ServerHelloMessage();
            serverHelloMessage.RandomNumber = new byte[RandomNumberLength];
            m_rng.GetBytes(serverHelloMessage.RandomNumber);

            SecurityParameters.ServerRandom = serverHelloMessage.RandomNumber;

            // in case their is no much the server will return this defaul
            serverHelloMessage.CipherSuite = CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA;

            foreach (CipherSuite cipherSuite in cipherSuites)
            {
                if (AllowedCipherSuites.Contains(cipherSuite))
                {
                    serverHelloMessage.CipherSuite = cipherSuite;
                    SetCipherSuite(cipherSuite);
                    break;
                }
            }

            NetMQMessage outgoingMessage = serverHelloMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ServerHello;
        }

        private void OnServerHello(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.HelloRequest || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Server Hello received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            ServerHelloMessage serverHelloMessage = new ServerHelloMessage();
            serverHelloMessage.SetFromNetMQMessage(incomingMessage);

            SecurityParameters.ServerRandom = serverHelloMessage.RandomNumber;

            SetCipherSuite(serverHelloMessage.CipherSuite);
        }

        private void OnCertificate(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.ServerHello || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Certificate received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            CertificateMessage certificateMessage = new CertificateMessage();
            certificateMessage.SetFromNetMQMessage(incomingMessage);

            if (!VerifyCertificate(certificateMessage.Certificate))
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Unable to verify certificate");
            }

            RemoteCertificate = certificateMessage.Certificate;
        }

        private void OnServerHelloDone(NetMQMessage incomingMessage,
                                      OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.Certificate || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Server Hello Done received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            ServerHelloDoneMessage serverHelloDoneMessage = new ServerHelloDoneMessage();
            serverHelloDoneMessage.SetFromNetMQMessage(incomingMessage);

            AddClientKeyExchange(outgoingMessages);

            InvokeChangeCipherSuite();

            AddFinished(outgoingMessages);
        }

        private void AddClientKeyExchange(OutgoingMessageBag outgoingMessages)
        {
            ClientKeyExchangeMessage clientKeyExchangeMessage = new ClientKeyExchangeMessage();

            byte[] premasterSecret = new byte[ClientKeyExchangeMessage.PreMasterSecretLength];
            m_rng.GetBytes(premasterSecret);

            RSACryptoServiceProvider rsa = RemoteCertificate.PublicKey.Key as RSACryptoServiceProvider;
            clientKeyExchangeMessage.EncryptedPreMasterSecret = rsa.Encrypt(premasterSecret, false);

            GenerateMasterSecret(premasterSecret);

            NetMQMessage outgoingMessage = clientKeyExchangeMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ClientKeyExchange;
        }

        private void OnClientKeyExchange(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.ClientHello || m_lastSentMessage != HandshakeType.ServerHelloDone)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Client Key Exchange received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            ClientKeyExchangeMessage clientKeyExchangeMessage = new ClientKeyExchangeMessage();
            clientKeyExchangeMessage.SetFromNetMQMessage(incomingMessage);

            RSACryptoServiceProvider rsa = LocalCertificate.PrivateKey as RSACryptoServiceProvider;

            byte[] premasterSecret = rsa.Decrypt(clientKeyExchangeMessage.EncryptedPreMasterSecret, false);

            GenerateMasterSecret(premasterSecret);

            InvokeChangeCipherSuite();
        }

        private void OnFinished(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (
                (SecurityParameters.Entity == ConnectionEnd.Client &&
                (!m_secureChannel.ChangeSuiteChangeArrived ||
                    m_lastReceivedMessage != HandshakeType.ServerHelloDone || m_lastSentMessage != HandshakeType.Finished)) ||
                (SecurityParameters.Entity == ConnectionEnd.Server &&
                (!m_secureChannel.ChangeSuiteChangeArrived ||
                m_lastReceivedMessage != HandshakeType.ClientKeyExchange || m_lastSentMessage != HandshakeType.ServerHelloDone)))
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Finished received when expecting another message");
            }

            if (SecurityParameters.Entity == ConnectionEnd.Server)
            {
                HashLocal(incomingMessage);
            }

            FinishedMessage finishedMessage = new FinishedMessage();
            finishedMessage.SetFromNetMQMessage(incomingMessage);

            m_remoteHash.TransformFinalBlock(new byte[0], 0, 0);

            byte[] seed = m_remoteHash.Hash;
            m_remoteHash.Dispose();
            m_remoteHash = null;

            string label;

            if (SecurityParameters.Entity == ConnectionEnd.Client)
            {
                label = ServerFinishedLabel;
            }
            else
            {
                label = ClientFinshedLabel;
            }

            byte[] verifyData =
              PRF.Get(SecurityParameters.MasterSecret, label, seed, FinishedMessage.VerificationDataLength);

            if (!verifyData.SequenceEqual(finishedMessage.VerificationData))
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeVerifyData, "peer verify data wrong");
            }

            if (SecurityParameters.Entity == ConnectionEnd.Server)
            {
                AddFinished(outgoingMessages);
            }

            m_done = true;
        }

        private void AddFinished(OutgoingMessageBag outgoingMessages)
        {
            m_localHash.TransformFinalBlock(new byte[0], 0, 0);

            byte[] seed = m_localHash.Hash;
            m_localHash.Dispose();
            m_localHash = null;

            string label;

            if (SecurityParameters.Entity == ConnectionEnd.Server)
            {
                label = ServerFinishedLabel;
            }
            else
            {
                label = ClientFinshedLabel;
            }

            FinishedMessage finishedMessage = new FinishedMessage();

            finishedMessage.VerificationData =
              PRF.Get(SecurityParameters.MasterSecret, label, seed, FinishedMessage.VerificationDataLength);

            NetMQMessage outgoingMessage = finishedMessage.ToNetMQMessage();
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.Finished;

            if (SecurityParameters.Entity == ConnectionEnd.Client)
            {
                HashRemote(outgoingMessage);
            }
        }

        private void SetCipherSuite(CipherSuite cipher)
        {
            switch (cipher)
            {
                case CipherSuite.TLS_NULL_WITH_NULL_NULL:
                case CipherSuite.TLS_RSA_WITH_NULL_SHA:
                case CipherSuite.TLS_RSA_WITH_NULL_SHA256:
                    SecurityParameters.BulkCipherAlgorithm = BulkCipherAlgorithm.Null;
                    SecurityParameters.FixedIVLength = 0;
                    SecurityParameters.EncKeyLength = 0;
                    SecurityParameters.BlockLength = 0;
                    SecurityParameters.RecordIVLength = 0;
                    break;
                case CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA:
                case CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256:
                    SecurityParameters.BulkCipherAlgorithm = BulkCipherAlgorithm.AES;
                    SecurityParameters.FixedIVLength = 0;
                    SecurityParameters.EncKeyLength = 16;
                    SecurityParameters.BlockLength = 16;
                    SecurityParameters.RecordIVLength = 16;
                    break;
                case CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA:
                case CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256:
                    SecurityParameters.BulkCipherAlgorithm = BulkCipherAlgorithm.AES;
                    SecurityParameters.FixedIVLength = 0;
                    SecurityParameters.EncKeyLength = 32;
                    SecurityParameters.BlockLength = 16;
                    SecurityParameters.RecordIVLength = 16;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("cipher");
            }

            switch (cipher)
            {
                case CipherSuite.TLS_NULL_WITH_NULL_NULL:
                    SecurityParameters.MACAlgorithm = MACAlgorithm.Null;
                    SecurityParameters.MACKeyLength = 0;
                    SecurityParameters.MACLength = 0;
                    break;
                case CipherSuite.TLS_RSA_WITH_NULL_SHA:
                case CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA:
                case CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA:
                    SecurityParameters.MACAlgorithm = MACAlgorithm.HMACSha1;
                    SecurityParameters.MACKeyLength = 20;
                    SecurityParameters.MACLength = 20;
                    break;
                case CipherSuite.TLS_RSA_WITH_NULL_SHA256:
                case CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256:
                case CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256:
                    SecurityParameters.MACAlgorithm = MACAlgorithm.HMACSha256;
                    SecurityParameters.MACKeyLength = 32;
                    SecurityParameters.MACLength = 32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("cipher");
            }
        }

        /// <summary>
        /// Raise the CipherSuiteChange event.
        /// </summary>
        private void InvokeChangeCipherSuite()
        {
            EventHandler temp = CipherSuiteChange;
            if (temp != null)
            {
                temp(this, EventArgs.Empty);
            }
        }

        private void GenerateMasterSecret(byte[] preMasterSecret)
        {
            byte[] seed = new byte[RandomNumberLength * 2];

            Buffer.BlockCopy(SecurityParameters.ClientRandom, 0, seed, 0, RandomNumberLength);
            Buffer.BlockCopy(SecurityParameters.ServerRandom, 0, seed, RandomNumberLength, RandomNumberLength);

            SecurityParameters.MasterSecret =
              PRF.Get(preMasterSecret, MasterSecretLabel, seed, MasterSecretLength);

            Array.Clear(preMasterSecret, 0, preMasterSecret.Length);
        }

        public void Dispose()
        {
            if (m_rng != null)
            {
                m_rng.Dispose();
                m_rng = null;
            }

            if (m_remoteHash != null)
            {
                m_remoteHash.Dispose();
                m_remoteHash = null;
            }

            if (m_localHash != null)
            {
                m_localHash.Dispose();
                m_localHash = null;
            }

            if (m_prf != null)
            {
                m_prf.Dispose();
                m_prf = null;
            }

        }
    }
}
