
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
        private Msg m_inProgress;

        private IMsgSource m_msgSource;

        public V2Encoder(int bufferSize, IMsgSource session, Endianness endian)
            : base(bufferSize, endian)
        {
            m_inProgress = new Msg();
            m_inProgress.InitEmpty();

            m_msgSource = session;

            // Write 0 bytes to the batch and go to message_ready state.
            NextStep(m_tmpbuf, 0, MessageReadyState, true);
        }

        public override void SetMsgSource(IMsgSource msgSource)
        {
            m_msgSource = msgSource;
        }

        protected override bool Next()
        {
            switch (State)
            {
                case SizeReadyState:
                    return SizeReady();
                case MessageReadyState:
                    return MessageReady();
                default:
                    return false;
            }
        }

        private bool SizeReady()
        {
            // Write message body into the buffer.
            NextStep(new ByteArraySegment(m_inProgress.Data, m_inProgress.Offset),
                m_inProgress.Size, MessageReadyState, !m_inProgress.HasMore);
            return true;
        }

        private bool MessageReady()
        {
            // Release the content of the old message.
            m_inProgress.Close();

            m_tmpbuf.Reset();

            // Read new message. If there is none, return false.
            // Note that new state is set only if write is successful. That way
            // unsuccessful write will cause retry on the next state machine
            // invocation.

            if (m_msgSource == null)
            {
                m_inProgress.InitEmpty();
                return false;
            }

            bool messagedPulled = m_msgSource.PullMsg(ref m_inProgress);
            if (!messagedPulled)
            {
                m_inProgress.InitEmpty();
                return false;
            }

            int protocolFlags = 0;
            if (m_inProgress.HasMore)
                protocolFlags |= V2Protocol.MoreFlag;
            if (m_inProgress.Size > 255)
                protocolFlags |= V2Protocol.LargeFlag;
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
            return true;
        }
    }
}
