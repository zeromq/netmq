using JetBrains.Annotations;

namespace NetMQ.Core.Transports
{
    internal class RawEncoder : EncoderBase
    {
        private IMsgSource m_msgSource;
        private Msg m_inProgress;

        private const int RawMessageSizeReadyState = 1;
        private const int RawMessageReadyState = 2;

        public RawEncoder(int bufferSize, [NotNull] IMsgSource msgSource, Endianness endianness) :
            base(bufferSize, endianness)
        {
            m_msgSource = msgSource;

            m_inProgress = new Msg();
            m_inProgress.InitEmpty();

            NextStep(null, 0, RawMessageReadyState, true);
        }

        public override void SetMsgSource(IMsgSource msgSource)
        {
            m_msgSource = msgSource;
        }

        protected override bool Next()
        {
            switch (State)
            {
                case RawMessageSizeReadyState:
                    return RawMessageSizeReady();
                case RawMessageReadyState:
                    return RawMessageReady();
                default:
                    return false;
            }
        }

        private bool RawMessageSizeReady()
        {
            // Write message body into the buffer.
            NextStep(new ByteArraySegment(m_inProgress.Data, m_inProgress.Offset),
                m_inProgress.Size, RawMessageReadyState, !m_inProgress.HasMore);
            return true;
        }

        private bool RawMessageReady()
        {
            // Destroy content of the old message.
            m_inProgress.Close();

            // Read new message. If there is none, return false.
            // Note that new state is set only if write is successful. That way
            // unsuccessful write will cause retry on the next state machine
            // invocation.
            if (m_msgSource == null)
                return false;

            bool result = m_msgSource.PullMsg(ref m_inProgress);

            if (!result)
            {
                m_inProgress.InitEmpty();
                return false;
            }

            m_inProgress.ResetFlags(MsgFlags.Shared | MsgFlags.More | MsgFlags.Identity);

            NextStep(null, 0, RawMessageSizeReadyState, true);

            return true;
        }
    }
}
