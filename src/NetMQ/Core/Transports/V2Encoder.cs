namespace NetMQ.Core.Transports
{
    /// <summary>
    /// Encoder for 0MQ framing protocol. Converts messages into data stream.
    /// </summary>
    internal class V2Encoder : EncoderBase
    {
        private const int SizeReadyState = 0;
        private const int MessageReadyState = 1;

        private readonly ByteArraySegment m_tmpbuf = new byte[9];
        
        public V2Encoder(int bufferSize, Endianness endian)
            : base(bufferSize, endian)
        {
            // Write 0 bytes to the batch and go to message_ready state.
            NextStep(m_tmpbuf, 0, MessageReadyState, true);
        }

        protected override void Next()
        {
            switch (State)
            {
                case SizeReadyState:
                    SizeReady();
                    break;
                    
                case MessageReadyState:
                    MessageReady();
                    break;
            }
        }

        private void SizeReady()
        {
            Assumes.NotNull(m_inProgress.UnsafeData);

            // Write message body into the buffer.
            NextStep(new ByteArraySegment(m_inProgress.UnsafeData, m_inProgress.UnsafeOffset),
                m_inProgress.Size, MessageReadyState, true);
        }

        private void MessageReady()
        {
            m_tmpbuf.Reset();
            
            int protocolFlags = 0;
            if (m_inProgress.HasMore)
                protocolFlags |= V2Protocol.MoreFlag;
            if (m_inProgress.Size > 255)
                protocolFlags |= V2Protocol.LargeFlag;
            if (m_inProgress.HasCommand)
                protocolFlags |= V2Protocol.CommandFlag;
            m_tmpbuf[0] = (byte)protocolFlags;

            // Encode the message length. For messages less then 256 bytes,
            // the length is encoded as 8-bit unsigned integer. For larger
            // messages, 64-bit unsigned integer in network byte order is used.
            int size = m_inProgress.Size;
            if (size > 255)
            {
                m_tmpbuf.PutLong(Endian, size, 1);
                NextStep(m_tmpbuf, 9, SizeReadyState, false);
            }
            else
            {
                m_tmpbuf[1] = (byte)(size);
                NextStep(m_tmpbuf, 2, SizeReadyState, false);
            }
        }
    }
}
