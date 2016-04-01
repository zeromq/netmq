namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The FinishedMessage is a HandshakeMessage with a HandshakeType of Finished.
    /// It holds a VerificationData property and a VerificationDataLength constant.
    /// </summary>
    internal class FinishedMessage : HandshakeMessage
    {
        /// <summary>
        /// The number of bytes within the verification-data (which is a byte-array).
        /// </summary>
        public const int VerifyDataLength = 12;

        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case, Finished.
        /// </summary>
        public override HandshakeType HandshakeType => HandshakeType.Finished;

        /// <summary>
        /// Get or set a byte-array that contains the verification data that is part of the finished-message.
        /// </summary>
        public byte[] VerifyData { get; set; }

        /// <summary>
        /// Remove the two frames from the given NetMQMessage, interpreting them thusly:
        /// 1. a byte with the HandshakeType,
        /// 2. a byte-array containing the verification data - used to verify the integrity of the content.
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

            NetMQFrame verifyDataFrame = message.Pop();

            VerifyData = verifyDataFrame.ToByteArray();
        }

        /// <summary>
        /// Return a new NetMQMessage that holds two frames:
        /// 1. a frame with a single byte representing the HandshakeType,
        /// 2. a frame containing the verification data.
        /// </summary>
        /// <returns>the resulting new NetMQMessage</returns>
        public override NetMQMessage ToNetMQMessage()
        {
            NetMQMessage message = base.ToNetMQMessage();
            message.Append(VerifyData);

            return message;
        }
    }
}
