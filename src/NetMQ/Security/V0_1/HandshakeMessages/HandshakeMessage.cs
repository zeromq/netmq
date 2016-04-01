namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// This enum-type specifies what part of the SSL/TLS handshake-protocol;
    /// it may be one any of 10 values such as HelloRequest,  CertificateVerify, or Finished.
    /// </summary>
    public enum HandshakeType : byte
    {
        /// <summary>This is what the client sends to initiate communication using the SSL handshake protocol.</summary>
        HelloRequest = 0,

        /// <summary>This is what the client sends to the server as it's initial step of the handshake-protocol.</summary>
        ClientHello = 1,

        /// <summary>This is that part of the handshake-protocol that the server sends to the client after that client has sent its client-hello message.</summary>
        ServerHello = 2,

        /// <summary>This denotes that part of the handshake-protocol in which a certificate is sent, along with the public key.</summary>
        Certificate = 11,

        /// <summary>This step is taken by the serve only when there is no public key shared along with the certificate.</summary>
        ServerKeyExchange = 12,

        /// <summary>This is that part of the handshake-protocol that the client sends when it needs to get authenticated by a client certificate.</summary>
        CertificateRequest = 13,

        /// <summary>This is sent by the server to tell the client that the server has finished sending its hello message and is waiting for a response from the client.</summary>
        ServerHelloDone = 14,

        /// <summary>This is that part of the handshake-protocol that denotes verification of a certificate.</summary>
        CertificateVerify = 15,

        /// <summary>This is only sent after the client calculates the premaster secret with the help of the random values of both the server and the client.</summary>
        ClientKeyExchange = 16,

        /// <summary>This is that part of the handshake-protocol that is sent to indicate readiness to exchange data</summary>
        Finished = 20
    }


    /// <summary>
    /// The abstract class HandshakeMessage holds a HandshakeType property and provides
    /// methods ToNetMQMessage and SetFromNetMQMessage, all intended to be overridden.
    /// </summary>
    internal abstract class HandshakeMessage
    {
        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents.
        /// </summary>
        public abstract HandshakeType HandshakeType { get; }

        /// <summary>
        /// Return a new NetMQMessage that holds a frame containing only one byte containing the HandshakeType.
        /// </summary>
        /// <returns>the HandshakeType wrapped in a new NetMQMessage</returns>
        public virtual NetMQMessage ToNetMQMessage()
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(new[] { (byte)HandshakeType });

            return message;
        }

        /// <summary>
        /// Remove the first frame from the given NetMQMessage.
        /// </summary>
        /// <param name="message">a NetMQMessage - which needs to have at least one frame</param>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.InvalidFramesCount"/>: FrameCount must not be 0.</exception>
        public virtual void SetFromNetMQMessage(NetMQMessage message)
        {
            if (message.FrameCount == 0)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
            }

            // remove the handshake type column
            message.Pop();
        }
    }
}
