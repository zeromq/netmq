
namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The FinishedMessage is a HandshakeMessage with a HandshakeType of Finished.
    /// It holds a VerificationData property and a VerificationDataLength constant.
    /// </summary>
    class FinishedMessage : HandshakeMessage
    {
        /// <summary>
        /// The number of bytes within the verification-data (which is a byte-array).
        /// </summary>
        public const int VerificationDataLength = 12;

        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case, Finished.
        /// </summary>
        public override HandshakeType HandshakeType
        {
            get { return HandshakeType.Finished; }
        }

        public byte[] VerificationData { get; set; }

        /// <summary>
        /// Remove the two frames from the given NetMQMessage, interpreting them thusly:
        /// 1. a byte with the HandshakeType,
        /// 2. a byte-array containing the verification data (CBL  What is that exactly?).
        /// </summary>
        /// <param name="message">a NetMQMessage - which must have 2 frames</param>
        public override void SetFromNetMQMessage(NetMQMessage message)
        {
            base.SetFromNetMQMessage(message);

            if (message.FrameCount != 1)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
            }

            NetMQFrame verificationDataFrame = message.Pop();

            VerificationData = verificationDataFrame.ToByteArray();
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
            message.Append(VerificationData);

            return message;
        }
    }
}
