using System;
using System.Diagnostics;

namespace NetMQ.Core.Transports
{
    internal class RawDecoder : DecoderBase
    {
        private Msg m_inProgress;

        private const int RawMessageReadyState = 1;
        private byte[] m_buffer;

        public RawDecoder(int bufferSize, long maxMsgSize, Endianness endianness)
            : base(bufferSize, endianness)
        {
            m_buffer = new byte[bufferSize];
        }

        public override void GetBuffer(out ByteArraySegment data, out int size)
        {
            data = new ByteArraySegment(m_buffer);
            size = m_buffer.Length;
        }

        public override DecodeResult Decode(ByteArraySegment data, int size, out int bytesUsed)
        {
            m_inProgress.InitPool(size);
            data.CopyTo(m_inProgress, size);
            bytesUsed = size;
            return DecodeResult.MessageReady;
        }

        protected override DecodeResult Next()
        {
            throw new NotImplementedException();
        }

        public override PushMsgResult PushMsg(ProcessMsgDelegate sink) => sink(ref m_inProgress);
    }
}
