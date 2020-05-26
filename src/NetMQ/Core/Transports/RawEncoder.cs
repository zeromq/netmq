#nullable disable

using JetBrains.Annotations;

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
            // Write message body into the buffer.
            NextStep(new ByteArraySegment(m_inProgress.UnsafeData, m_inProgress.UnsafeOffset),
                m_inProgress.Size, RawMessageReadyState, true);
        }
    }
}
