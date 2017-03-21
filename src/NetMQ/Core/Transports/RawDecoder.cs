using System.Diagnostics;

namespace NetMQ.Core.Transports
{
    internal class RawDecoder : DecoderBase
    {
        private readonly long m_maxMsgSize;

        private IMsgSink m_msgSink;
        private Msg m_inProgress;

        private const int RawMessageReadyState = 1;

        public RawDecoder(int bufferSize, long maxMsgSize, IMsgSink msgSink,
          Endianness endianness)
            : base(bufferSize, endianness)
        {
            m_msgSink = msgSink;
            m_maxMsgSize = maxMsgSize;
        }

        public override void SetMsgSink(IMsgSink msgSink)
        {
            m_msgSink = msgSink;
        }

        public override bool Stalled()
        {
            return false;
        }

        public override bool MessageReadySize(int msgSize)
        {
            m_inProgress = new Msg();
            m_inProgress.InitPool(msgSize);

            NextStep(new ByteArraySegment(m_inProgress.Data, m_inProgress.Offset),
                m_inProgress.Size, RawMessageReadyState);

            return true;
        }

        protected override bool Next()
        {
            if (State == RawMessageReadyState)
            {
                return RawMessageReady();
            }

            return false;
        }

        private bool RawMessageReady()
        {
            Debug.Assert(m_inProgress.Size != 0);

            // Message is completely read. Push it further and start reading
            // new message. (in_progress is a 0-byte message after this point.)
            if (m_msgSink == null)
                return false;

            try
            {

                bool isMessagedPushed = m_msgSink.PushMsg(ref m_inProgress);

                if (isMessagedPushed)
                {
                    // NOTE: This is just to break out of process_buffer
                    // raw_message_ready should never get called in state machine w/o
                    // message_ready_size from stream_engine.
                    NextStep(new ByteArraySegment(m_inProgress.Data, m_inProgress.Offset),
                        1, RawMessageReadyState);
                }

                return isMessagedPushed;
            }
            catch (NetMQException)
            {
                DecodingError();
                return false;
            }
        }
    }
}
