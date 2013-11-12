
namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The ClientKeyExchangeMessage is a HandshakeMessage with a HandshakeType of ClientKeyExchange.
    /// It holds a EncryptedPreMasterSecret,
    /// and overrides SetFromNetMQMessage/ToNetMQMessage to read/write that
    /// from the frames of a NetMQMessage.
    /// </summary>
    class ClientKeyExchangeMessage : HandshakeMessage
    {
        /// <summary>
        /// The number of bytes within the EncryptedPreMasterSecret.
        /// </summary>
        public const int PreMasterSecretLength = 48;

        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case a ClientKeyExchange.
        /// </summary>
        public override HandshakeType HandshakeType
        {
            get { return HandshakeType.ClientKeyExchange; }
        }

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
