
namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// This enum-type specifies what part of the handshake-protocol;
    /// it may be one any of 10 values such as HelloRequest,  CertificateVerify, or Finished.
    /// </summary>
    public enum HandshakeType : byte
    {
        HelloRequest = 0,
        ClientHello = 1,
        ServerHello = 2,
        Certificate = 11,
        ServerKeyExchange = 12,
        CertificateRequest = 13,
        ServerHelloDone = 14,
        CertificateVerify = 15,
        ClientKeyExchange = 16,
        Finished = 20
    }


    /// <summary>
    /// The abstract class HandshakeMessage holds a HandshakeType property and provides
    /// methods ToNetMQMessage and SetFromNetMQMessage, all intended to be overridden.
    /// </summary>
    abstract class HandshakeMessage
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
            message.Append(new byte[] { (byte)HandshakeType });

            return message;
        }

        /// <summary>
        /// Remove the first frame from the given NetMQMessage.
        /// </summary>
        /// <param name="message">a NetMQMessage - which needs to have at least one frame</param>
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
