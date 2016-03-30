namespace NetMQ.Security.V0_1.HandshakeMessages
{
    /// <summary>
    /// The ServerHelloMessage is a HandshakeMessage with a <see cref="HandshakeType"/>of ServerHello.
    /// It holds a RandomNumber and a <see cref="CipherSuite"/>, both of which are gleaned from
    /// a NetMQMessage in the override of SetFromNetMQMessage.
    /// </summary>
    internal class ServerHelloMessage : HandshakeMessage
    {
        /// <summary>
        /// Get the part of the handshake-protocol that this HandshakeMessage represents
        /// - in this case, ServerHello.
        /// </summary>
        public override HandshakeType HandshakeType => HandshakeType.ServerHello;

        /// <summary>
        /// Get or set the Random-Number that is a part of the handshake-protocol, as a byte-array.
        /// </summary>
        public byte[] RandomNumber { get; set; }

        /// <summary>
        /// Get or set the byte that specifies the cipher-suite to be used.
        /// </summary>
        public CipherSuite CipherSuite { get; set; }

        /// <summary>
        /// Remove the three frames from the given NetMQMessage, interpreting them thusly:
        /// 1. a byte with the <see cref="HandshakeType"/>,
        /// 2. RandomNumber (a byte-array),
        /// 3. a 2-byte array with the <see cref="CipherSuite"/> in the 2nd byte.
        /// </summary>
        /// <param name="message">a NetMQMessage - which must have 3 frames</param>
        /// <exception cref="NetMQSecurityException"><see cref="NetMQSecurityErrorCode.InvalidFramesCount"/>: FrameCount must be 2.</exception>
        public override void SetFromNetMQMessage(NetMQMessage message)
        {
            base.SetFromNetMQMessage(message);

            if (message.FrameCount != 2)
            {
                throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
            }

            // Get the random number
            NetMQFrame randomNumberFrame = message.Pop();
            RandomNumber = randomNumberFrame.ToByteArray();

            // Get the cipher suite
            NetMQFrame cipherSuiteFrame = message.Pop();
            CipherSuite = (CipherSuite)cipherSuiteFrame.Buffer[1];
        }

        /// <summary>
        /// Return a new NetMQMessage that holds three frames:
        /// 1. contains a byte with the <see cref="HandshakeType"/>,
        /// 2. contains the RandomNumber (a byte-array),
        /// 3. contains a 2-byte array containing zero, and a byte representing the CipherSuite.
        /// </summary>
        /// <returns>the resulting new NetMQMessage</returns>
        public override NetMQMessage ToNetMQMessage()
        {
            NetMQMessage message = base.ToNetMQMessage();
            message.Append(RandomNumber);
            message.Append(new byte[] { 0, (byte)CipherSuite });

            return message;
        }
    }
}
