using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetMQ.Security.V0_1.HandshakeMessages;

namespace NetMQ.Security.V0_1
{
    internal class HandshakeLayer : IDisposable
    {
        /// <summary>
        /// This is the SecureChannel that this handshake-protocol is communicating over.
        /// </summary>
        private readonly SecureChannel m_secureChannel;

        /// <summary>
        /// This denotes the length of the byte-array that holds the random-number value.
        /// </summary>
        public const int RandomNumberLength = 32;

        /// <summary>
        /// This denotes the length of the byte-array that holds the master-secret.
        /// </summary>
        public const int MasterSecretLength = 48;

        /// <summary>
        /// This is simply a string literal containing "master secret".
        /// </summary>
        public string MasterSecretLabel = "master secret";

        /// <summary>
        /// This is simply a string literal containing "client finished".
        /// </summary>
        public string ClientFinshedLabel = "client finished";

        /// <summary>
        /// This is simply a string literal containing "server finished".
        /// </summary>
        public string ServerFinishedLabel = "server finished";

        /// <summary>
        /// This serves to remember which HandshakeType was last received.
        /// </summary>
        private HandshakeType m_lastReceivedMessage = HandshakeType.HelloRequest;

        /// <summary>
        /// This serves to remember which HandshakeType was last sent.
        /// </summary>
        private HandshakeType m_lastSentMessage = HandshakeType.HelloRequest;

        /// <summary>
        /// This is the local hash-calculator, as opposed to the hash-calculator for the remote-peer.
        /// It uses the SHA-256 algorithm (SHA stands for Standard Hashing Algorithm).
        /// </summary>
        private SHA256 m_localHash;

        /// <summary>
        /// This is the hash-calculator for the remote peer.
        /// It uses the SHA-256 algorithm (SHA stands for Standard Hashing Algorithm).
        /// </summary>
        private SHA256 m_remoteHash;

        /// <summary>
        /// This is the random-number-generator that is used to create cryptographically-strong random byte-array data.
        /// </summary>
        private RandomNumberGenerator m_rng = new RNGCryptoServiceProvider();

        /// <summary>
        /// This flag indicates when the handshake has finished. It is set true in method OnFinished.
        /// </summary>
        private bool m_done;

        /// <summary>
        /// This is the Pseudo-Random number generating-Function (PRF) that is being used.
        /// It is initialized to a SHA256PRF.
        /// </summary>
        private IPRF m_prf = new SHA256PRF();

        /// <summary>
        /// Create a new HandshakeLayer object given a SecureChannel and which end of the connection it is to be.
        /// </summary>
        /// <param name="secureChannel">the SecureChannel that comprises the secure functionality of this layer</param>
        /// <param name="connectionEnd">this specifies which end of the connection - Server or Client</param>
        public HandshakeLayer(SecureChannel secureChannel, ConnectionEnd connectionEnd)
        {
            // SHA256 is a class that computes the SHA-256 (SHA stands for Standard Hashing Algorithm) of it's input.
            m_localHash = SHA256.Create();
            m_remoteHash = SHA256.Create();

            m_secureChannel = secureChannel;
            SecurityParameters = new SecurityParameters
            {
                Entity = connectionEnd,
                CompressionAlgorithm = CompressionMethod.Null,
                PRFAlgorithm = PRFAlgorithm.SHA256,
                CipherType = CipherType.Block
            };

            AllowedCipherSuites = new[]
            {
                CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256,
                CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA,
                CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256,
                CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA
            };

            VerifyCertificate = c => c.Verify();
        }

        public SecurityParameters SecurityParameters { get; }

        /// <summary>
        /// Get or set the array of allowed cipher-suites.
        /// </summary>
        public CipherSuite[] AllowedCipherSuites { get; set; }

        /// <summary>
        /// Get or set the local X.509-certificate.
        /// </summary>
        public X509Certificate2 LocalCertificate { get; set; }

        /// <summary>
        /// Get or set the remote X.509-certificate.
        /// </summary>
        public X509Certificate2 RemoteCertificate { get; set; }

        /// <summary>
        /// Get the Pseudo-Random number generating-Function (PRF) that is being used.
        /// </summary>
        public IPRF PRF => m_prf;

        /// <summary>
        /// This event signals a change to the cipher-suite.
        /// </summary>
        public event EventHandler CipherSuiteChange;

        /// <summary>
        /// Get or set the delegate to use to call the method for verifying the certificate.
        /// </summary>
        public VerifyCertificateDelegate VerifyCertificate { get; set; }

        /// <summary>
        /// Given an incoming handshake-protocol message, route it to the corresponding handler.
        /// </summary>
        /// <param name="incomingMessage">the NetMQMessage that has come in</param>
        /// <param name="outgoingMessages">a collection of NetMQMessages that are to be sent</param>
        /// <returns>true if finished - ie, an incoming message of type Finished was received</returns>
        /// <exception cref="ArgumentNullException"><paramref name="incomingMessage"/> must not be <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="incomingMessage"/> must have a valid <see cref="HandshakeType"/>.</exception>
        public bool ProcessMessages(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (incomingMessage == null)
            {
                if (m_lastReceivedMessage == m_lastSentMessage &&
                    m_lastSentMessage == HandshakeType.HelloRequest &&
                    SecurityParameters.Entity == ConnectionEnd.Client)
                {
                    OnHelloRequest(outgoingMessages);
                    return false;
                }
                else
                {
                    throw new ArgumentNullException(nameof(incomingMessage));
                }
            }

            var handshakeType = (HandshakeType)incomingMessage[0].Buffer[0];

            switch (handshakeType)
            {
                case HandshakeType.ClientHello:
                    OnClientHello(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.ServerHello:
                    OnServerHello(incomingMessage);
                    break;
                case HandshakeType.Certificate:
                    OnCertificate(incomingMessage);
                    break;
                case HandshakeType.ServerHelloDone:
                    OnServerHelloDone(incomingMessage, outgoingMessages);
                    break;
                case HandshakeType.ClientKeyExchange:
                    OnClientKeyExchange(incomingMessage);
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
        /// <param name="hash">the hashing-algorithm to employ</param>
        /// <param name="message">the NetMQMessage whose frames are to be hashed</param>
        private static void Hash(HashAlgorithm hash, NetMQMessage message)
        {
            foreach (var frame in message)
            {
                // Access the byte-array that is the frame's buffer.
                byte[] bytes = frame.ToByteArray(true);

                // Compute the hash value for the region of the input byte-array (bytes), starting at index 0,
                // and copy the resulting hash value back into the same byte-array.
                hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }
        }

        private void OnHelloRequest(OutgoingMessageBag outgoingMessages)
        {
            var clientHelloMessage = new ClientHelloMessage { RandomNumber = new byte[RandomNumberLength] };

            m_rng.GetBytes(clientHelloMessage.RandomNumber);

            SecurityParameters.ClientRandom = clientHelloMessage.RandomNumber;

            clientHelloMessage.CipherSuites = AllowedCipherSuites;

            NetMQMessage outgoingMessage = clientHelloMessage.ToNetMQMessage();

            HashLocalAndRemote(outgoingMessage);

            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ClientHello;
        }

        /// <exception cref="NetMQSecurityException">The client hello message must not be received while expecting a different message.</exception>
        private void OnClientHello(NetMQMessage incomingMessage, OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.HelloRequest || m_lastSentMessage != HandshakeType.HelloRequest)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Client Hello received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            var clientHelloMessage = new ClientHelloMessage();
            clientHelloMessage.SetFromNetMQMessage(incomingMessage);

            SecurityParameters.ClientRandom = clientHelloMessage.RandomNumber;

            AddServerHelloMessage(outgoingMessages, clientHelloMessage.CipherSuites);

            AddCertificateMessage(outgoingMessages);

            AddServerHelloDone(outgoingMessages);
        }

        private void AddServerHelloDone(OutgoingMessageBag outgoingMessages)
        {
            var serverHelloDoneMessage = new ServerHelloDoneMessage();
            NetMQMessage outgoingMessage = serverHelloDoneMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ServerHelloDone;
        }

        private void AddCertificateMessage(OutgoingMessageBag outgoingMessages)
        {
            var certificateMessage = new CertificateMessage { Certificate = LocalCertificate };

            NetMQMessage outgoingMessage = certificateMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.Certificate;
        }

        private void AddServerHelloMessage(OutgoingMessageBag outgoingMessages, CipherSuite[] cipherSuites)
        {
            var serverHelloMessage = new ServerHelloMessage { RandomNumber = new byte[RandomNumberLength] };
            m_rng.GetBytes(serverHelloMessage.RandomNumber);

            SecurityParameters.ServerRandom = serverHelloMessage.RandomNumber;

            // in case there is no match the server will return this default
            serverHelloMessage.CipherSuite = CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA;

            foreach (var cipherSuite in cipherSuites)
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

        /// <exception cref="NetMQSecurityException">The server hello message must not be received while expecting a different message.</exception>
        private void OnServerHello(NetMQMessage incomingMessage)
        {
            if (m_lastReceivedMessage != HandshakeType.HelloRequest || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Server Hello received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            var serverHelloMessage = new ServerHelloMessage();
            serverHelloMessage.SetFromNetMQMessage(incomingMessage);

            SecurityParameters.ServerRandom = serverHelloMessage.RandomNumber;

            SetCipherSuite(serverHelloMessage.CipherSuite);
        }

        /// <exception cref="NetMQSecurityException">Must be able to verify the certificate.</exception>
        /// <exception cref="NetMQSecurityException">The certificate message must not be received while expecting a another message.</exception>
        private void OnCertificate(NetMQMessage incomingMessage)
        {
            if (m_lastReceivedMessage != HandshakeType.ServerHello || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Certificate received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            var certificateMessage = new CertificateMessage();
            certificateMessage.SetFromNetMQMessage(incomingMessage);

            if (!VerifyCertificate(certificateMessage.Certificate))
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Unable to verify certificate");
            }

            RemoteCertificate = certificateMessage.Certificate;
        }

        /// <exception cref="NetMQSecurityException">The server hello message must not be received while expecting another message.</exception>
        private void OnServerHelloDone(NetMQMessage incomingMessage,
            OutgoingMessageBag outgoingMessages)
        {
            if (m_lastReceivedMessage != HandshakeType.Certificate || m_lastSentMessage != HandshakeType.ClientHello)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Server Hello Done received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            var serverHelloDoneMessage = new ServerHelloDoneMessage();
            serverHelloDoneMessage.SetFromNetMQMessage(incomingMessage);

            AddClientKeyExchange(outgoingMessages);

            InvokeChangeCipherSuite();

            AddFinished(outgoingMessages);
        }

        private void AddClientKeyExchange(OutgoingMessageBag outgoingMessages)
        {
            var clientKeyExchangeMessage = new ClientKeyExchangeMessage();

            var premasterSecret = new byte[ClientKeyExchangeMessage.PreMasterSecretLength];
            m_rng.GetBytes(premasterSecret);

            var rsa = RemoteCertificate.PublicKey.Key as RSACryptoServiceProvider;
            clientKeyExchangeMessage.EncryptedPreMasterSecret = rsa.Encrypt(premasterSecret, false);

            GenerateMasterSecret(premasterSecret);

            NetMQMessage outgoingMessage = clientKeyExchangeMessage.ToNetMQMessage();
            HashLocalAndRemote(outgoingMessage);
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.ClientKeyExchange;
        }

        /// <exception cref="NetMQSecurityException">The client key exchange must not be received while expecting a another message.</exception>
        private void OnClientKeyExchange(NetMQMessage incomingMessage)
        {
            if (m_lastReceivedMessage != HandshakeType.ClientHello || m_lastSentMessage != HandshakeType.ServerHelloDone)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.HandshakeUnexpectedMessage, "Client Key Exchange received when expecting another message");
            }

            HashLocalAndRemote(incomingMessage);

            var clientKeyExchangeMessage = new ClientKeyExchangeMessage();
            clientKeyExchangeMessage.SetFromNetMQMessage(incomingMessage);

            var rsa = LocalCertificate.PrivateKey as RSACryptoServiceProvider;

            byte[] premasterSecret = rsa.Decrypt(clientKeyExchangeMessage.EncryptedPreMasterSecret, false);

            GenerateMasterSecret(premasterSecret);

            InvokeChangeCipherSuite();
        }

        /// <exception cref="NetMQSecurityException">The Finished message must not be received while expecting a another message.</exception>
        /// <exception cref="NetMQSecurityException">The peer verification data must be valid.</exception>
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

            var finishedMessage = new FinishedMessage();
            finishedMessage.SetFromNetMQMessage(incomingMessage);

            m_remoteHash.TransformFinalBlock(EmptyArray<byte>.Instance, 0, 0);

            byte[] seed = m_remoteHash.Hash;
            m_remoteHash.Dispose();
            m_remoteHash = null;

            var label = SecurityParameters.Entity == ConnectionEnd.Client ? ServerFinishedLabel : ClientFinshedLabel;

            var verifyData = PRF.Get(SecurityParameters.MasterSecret, label, seed, FinishedMessage.VerifyDataLength);

            if (!verifyData.SequenceEqual(finishedMessage.VerifyData))
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
            m_localHash.TransformFinalBlock(EmptyArray<byte>.Instance, 0, 0);

            byte[] seed = m_localHash.Hash;
            m_localHash.Dispose();
            m_localHash = null;

            var label = SecurityParameters.Entity == ConnectionEnd.Server ? ServerFinishedLabel : ClientFinshedLabel;

            var finishedMessage = new FinishedMessage
            {
                VerifyData = PRF.Get(SecurityParameters.MasterSecret, label, seed, FinishedMessage.VerifyDataLength)
            };

            NetMQMessage outgoingMessage = finishedMessage.ToNetMQMessage();
            outgoingMessages.AddHandshakeMessage(outgoingMessage);
            m_lastSentMessage = HandshakeType.Finished;

            if (SecurityParameters.Entity == ConnectionEnd.Client)
            {
                HashRemote(outgoingMessage);
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">cipher must have a valid value.</exception>
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
                    throw new ArgumentOutOfRangeException(nameof(cipher));
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
                    throw new ArgumentOutOfRangeException(nameof(cipher));
            }
        }

        /// <summary>
        /// Raise the CipherSuiteChange event.
        /// </summary>
        private void InvokeChangeCipherSuite()
        {
            CipherSuiteChange?.Invoke(this, EventArgs.Empty);
        }

        private void GenerateMasterSecret(byte[] preMasterSecret)
        {
            var seed = new byte[RandomNumberLength*2];

            Buffer.BlockCopy(SecurityParameters.ClientRandom, 0, seed, 0, RandomNumberLength);
            Buffer.BlockCopy(SecurityParameters.ServerRandom, 0, seed, RandomNumberLength, RandomNumberLength);

            SecurityParameters.MasterSecret =
                PRF.Get(preMasterSecret, MasterSecretLabel, seed, MasterSecretLength);

            Array.Clear(preMasterSecret, 0, preMasterSecret.Length);
        }

        /// <summary>
        /// Dispose of any contained resources.
        /// </summary>
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