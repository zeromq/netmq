using System.Security.Cryptography.X509Certificates;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The CertificateMessage is a type of HandshakeMessage with a HandshakeType of Certificate.
    /// It holds a Certificate, and overrides SetFromNetMQMessage and ToNetMQMessage to read/write the certificate
    /// from the frames of a NetMQMessage.
    /// </summary>
    internal class CertificateMessage : HandshakeMessage
    {
        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case a Certificate.
        /// </summary>
        public override HandshakeType HandshakeType => HandshakeType.Certificate;

        /// <summary>
        /// Get or set the X.509 Digital Certificate that this message contains.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Remove the two frames from the given NetMQMessage, interpreting them thusly:
        /// 1. a byte with the HandshakeType,
        /// 2. a byte-array containing the X.509 digital certificate.
        /// </summary>
        /// <param name="message">a NetMQMessage - which must have 2 frames</param>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.InvalidFramesCount"/>: FrameCount must be 1.</exception>
        public override void SetFromNetMQMessage(NetMQMessage message)
        {
            base.SetFromNetMQMessage(message);

            if (message.FrameCount != 1)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
            }

            NetMQFrame certificateFrame = message.Pop();

            byte[] certificateBytes = certificateFrame.ToByteArray();

            Certificate = new X509Certificate2();
            Certificate.Import(certificateBytes);
        }

        /// <summary>
        /// Return a new NetMQMessage that holds two frames:
        /// 1. a frame with a single byte representing the HandshakeType, which is Certificate,
        /// 2. a frame containing the certificate that has been exported to a byte-array.
        /// </summary>
        /// <returns>the resulting new NetMQMessage</returns>
        public override NetMQMessage ToNetMQMessage()
        {
            NetMQMessage message = base.ToNetMQMessage();

            message.Append(Certificate.Export(X509ContentType.Cert));

            return message;
        }
    }
}
