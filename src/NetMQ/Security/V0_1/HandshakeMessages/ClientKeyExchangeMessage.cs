namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The ClientKeyExchangeMessage is a HandshakeMessage with a HandshakeType of ClientKeyExchange.
    /// It holds a EncryptedPreMasterSecret,
    /// and overrides SetFromNetMQMessage/ToNetMQMessage to read/write that
    /// from the frames of a <see cref="NetMQMessage"/>.
    /// </summary>
    internal class ClientKeyExchangeMessage : HandshakeMessage
    {
        /// <summary>
        /// The number of bytes within the EncryptedPreMasterSecret.
        /// </summary>
        public const int PreMasterSecretLength = 48;

        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case a ClientKeyExchange.
        /// </summary>
        public override HandshakeType HandshakeType => HandshakeType.ClientKeyExchange;

        /// <summary>
        /// Get or set the 48-byte array that is the encrypted pre-master secret.
        /// </summary>
        public byte[] EncryptedPreMasterSecret { get; set; }

        /// <summary>
        /// Return a new NetMQMessage that holds two frames:
        /// 1. a frame with a single byte representing the HandshakeType, which is ClientKeyExchange,
        /// 2. a frame containing the EncryptedPreMasterSecret.
        /// </summary>
        /// <returns>the resulting new NetMQMessage</returns>
        public override NetMQMessage ToNetMQMessage()
        {
            NetMQMessage message = base.ToNetMQMessage();
            message.Append(EncryptedPreMasterSecret);

            return message;
        }

        /// <summary>
        /// Remove the two frames from the given NetMQMessage, interpreting them thusly:
        /// 1. a byte with the HandshakeType, assumed to be ClientKeyExchange
        /// 2. a byte-array containing the EncryptedPreMasterSecret.
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

            NetMQFrame preMasterSecretFrame = message.Pop();

            EncryptedPreMasterSecret = preMasterSecretFrame.ToByteArray();
        }
    }
}
