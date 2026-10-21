using NetMQ.Core;
using NetMQ.Core.Transports;
using Xunit;

namespace NetMQ.Tests
{
    public class EncoderTests
    {
        [Fact]
        public void V1EmptyFrame()
        {
            using var encoder = new V1Encoder(16, Endianness.Big);
            EmptyFrame(encoder, false, new byte[] { 1, 0 }, new byte[] { 2, 0, 42 });
            EmptyFrame(encoder, true, new byte[] { 1, 1 }, new byte[] { 2, 0, 42 });
        }

        [Fact]
        public void V2EmptyFrame()
        {
            using var encoder = new V2Encoder(16, Endianness.Big);
            EmptyFrame(encoder, false, new byte[] { 0, 0 }, new byte[] { 0, 1, 42 });
            EmptyFrame(encoder, true, new byte[] { 1, 0 }, new byte[] { 0, 1, 42 });
        }

        [Fact]
        public void RawEmptyFrame()
        {
            using var encoder = new RawEncoder(16, Endianness.Big);
            EmptyFrame(encoder, false, new byte[0], new byte[] { 42 });
            EmptyFrame(encoder, true, new byte[0], new byte[] { 42 });
        }

        private static void EmptyFrame(EncoderBase encoder, bool more, byte[] expected, byte[] nextExpected)
        {
            var msg = new Msg();
            msg.InitEmpty();
            if (more)
                msg.SetFlags(MsgFlags.More);

            encoder.LoadMsg(ref msg);
            Assert.Equal(expected, Encode(encoder));

            msg.InitGC(new byte[] { 42 }, 1);
            encoder.LoadMsg(ref msg);
            Assert.Equal(nextExpected, Encode(encoder));
            msg.Close();
        }

        private static byte[] Encode(EncoderBase encoder)
        {
            ByteArraySegment? data = null;
            int size = encoder.Encode(ref data, 16);
            var bytes = new byte[size];
            data!.CopyTo(new ByteArraySegment(bytes), size);
            Assert.Equal(0, encoder.Encode(ref data, 16));
            return bytes;
        }
    }
}
