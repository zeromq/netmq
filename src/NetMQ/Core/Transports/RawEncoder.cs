namespace NetMQ.Core.Transports
{
    internal class RawEncoder : EncoderBase
    {
        private const int RawMessageReadyState = 1;

        public RawEncoder(int bufferSize, Endianness endianness) :
            base(bufferSize, endianness)
        {
            NextStep(null, 0, RawMessageReadyState, true);
        }

        protected override void Next()
        {
            if (m_inProgress.Size == 0)
            {
                NextStep(null, 0, RawMessageReadyState, true);
                return;
            }

            Assumes.NotNull(m_inProgress.UnsafeData);

            // Write message body into the buffer.
            NextStep(new ByteArraySegment(m_inProgress.UnsafeData, m_inProgress.UnsafeOffset),
                m_inProgress.Size, RawMessageReadyState, true);
        }
    }
}
