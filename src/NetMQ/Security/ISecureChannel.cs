using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetMQ.Security
{
    /// <summary>
    /// This delegate defines a method signature that takes a PkixCertificate and returns a bool.
    /// </summary>
    /// <param name="certificate2">the PkixCertificate that is to do the verification</param>
    /// <returns>the result of the verification - which is true if it verifies ok, false if it fails verification</returns>
    public delegate bool VerifyCertificateDelegate(X509Certificate2 certificate2);

    /// <summary>
    /// An ISecureChannel provides a secure communication channel between a client and a server.
    /// It provides for a X.509 certificate, and methods to process, encrypt, and decrypt messages.
    /// </summary>
    public interface ISecureChannel : IDisposable
    {
        /// <summary>
        /// Get whether the secure channel is ready to encrypt messages.
        /// </summary>
        bool SecureChannelReady { get; }

        /// <summary>
        /// Get or set the certificate of the server; for client this property is irrelevant.
        /// The certificate must include a private key.
        /// </summary>
        X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Get or set the array of allowed cipher suites for this secure channel, ordered by priority.
        /// </summary>
        CipherSuite[] AllowedCipherSuites { get; set; }

        /// <summary>
        /// Set the verify-certificate method. By default the certificate is validated by the certificate chain.
        /// </summary>
        /// <param name="verifyCertificate">Delegate for the verify certificate method</param>
        void SetVerifyCertificate(VerifyCertificateDelegate verifyCertificate);

        /// <summary>
        /// Process handshake and change cipher suite messages. This method should be called for every incoming message until the method returns true.
        /// You cannot encrypt or decrypt messages until the method return true.
        /// Each call to the method may include outgoing messages that need to be sent to the other peer.
        /// </summary>
        /// <param name="incomingMessage">The incoming message from the other peer</param>
        /// <param name="outgoingMesssages">outgoing messages that need to be sent to the other peer</param>
        /// <returns>Return true when the method completes the handshake stage and the SecureChannel is ready to encrypt and decrypt messages</returns>
        bool ProcessMessage(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMesssages);

        /// <summary>
        /// Encrypt the given application message and return the encrypted version of it.
        /// </summary>
        /// <param name="plainMessage">The plain-text message to encrypt</param>
        /// <returns>The encrypted cipher message</returns>
        NetMQMessage EncryptApplicationMessage(NetMQMessage plainMessage);

        /// <summary>
        /// Decrypt the given cipher message (a <see cref="NetMQMessage"/> that has been encrypted) and return the decrypted version of it.
        /// </summary>
        /// <param name="cipherMessage">The cipher message to decrypt</param>
        /// <returns>the decrypted message</returns>
        NetMQMessage DecryptApplicationMessage(NetMQMessage cipherMessage);
    }
}