
namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The ServerHelloDoneMessage is a HandshakeMessage with a HandshakeType of ServerHelloDone.
    /// </summary>
    class ServerHelloDoneMessage : HandshakeMessage
    {
        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case, ServerHelloDone.
        /// </summary>
        public override HandshakeType HandshakeType
        {
            get { return HandshakeType.ServerHelloDone; }
        }

        /// <summary>
        /// Remove the one frame from the given NetMQMessage, which shall contain one byte with the HandshakeType,
        /// presumed here to be ServerHelloDone.
        /// </summary>
        /// <param name="message">a NetMQMessage - which must have 1 frame</param>
        public override void SetFromNetMQMessage(NetMQMessage message)
        {
            base.SetFromNetMQMessage(message);

            if (message.FrameCount != 0)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
            }
        }
    }
}
