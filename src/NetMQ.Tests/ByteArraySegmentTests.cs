using NetMQ.Core.Transports;
using Xunit;

namespace NetMQ.Tests
{
    public class ByteArraySegmentTests
    {
        [Fact]
        public void LongLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[8]);

            byteArraySegment.PutLong(Endianness.Little, 1, 0);

            Assert.Equal(byteArraySegment[0], 1);
            Assert.Equal(0, byteArraySegment[7]);

            long num = byteArraySegment.GetLong(Endianness.Little, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutLong(Endianness.Little, 72057594037927936, 0);

            Assert.Equal(1, byteArraySegment[7]);
            Assert.Equal(0, byteArraySegment[0]);

            num = byteArraySegment.GetLong(Endianness.Little, 0);

            Assert.Equal(72057594037927936, num);
        }

        [Fact]
        public void LongSmallEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[8]);

            byteArraySegment.PutLong(Endianness.Big, 1, 0);

            Assert.Equal(byteArraySegment[7], 1);
            Assert.Equal(0, byteArraySegment[0]);

            long num = byteArraySegment.GetLong(Endianness.Big, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutLong(Endianness.Big, 72057594037927936, 0);

            Assert.Equal(1, byteArraySegment[0]);
            Assert.Equal(0, byteArraySegment[7]);

            num = byteArraySegment.GetLong(Endianness.Big, 0);

            Assert.Equal(72057594037927936, num);
        }

        [Fact]
        public void IntLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[4]);

            byteArraySegment.PutInteger(Endianness.Little, 1, 0);

            Assert.Equal(1, byteArraySegment[0]);
            Assert.Equal(0, byteArraySegment[3]);

            long num = byteArraySegment.GetInteger(Endianness.Little, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutInteger(Endianness.Little, 16777216, 0);

            Assert.Equal(1, byteArraySegment[3]);
            Assert.Equal(0, byteArraySegment[0]);

            num = byteArraySegment.GetInteger(Endianness.Little, 0);

            Assert.Equal(16777216, num);
        }

        [Fact]
        public void IntBigEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[4]);

            byteArraySegment.PutInteger(Endianness.Big, 1, 0);

            Assert.Equal(1, byteArraySegment[3]);
            Assert.Equal(0, byteArraySegment[0]);

            long num = byteArraySegment.GetInteger(Endianness.Big, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutInteger(Endianness.Big, 16777216, 0);

            Assert.Equal(1, byteArraySegment[0]);
            Assert.Equal(0, byteArraySegment[3]);

            num = byteArraySegment.GetInteger(Endianness.Big, 0);

            Assert.Equal(16777216, num);
        }

        [Fact]
        public void UnsignedShortLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[2]);

            byteArraySegment.PutUnsignedShort(Endianness.Little, 1, 0);

            Assert.Equal(1, byteArraySegment[0]);
            Assert.Equal(0, byteArraySegment[1]);

            long num = byteArraySegment.GetUnsignedShort(Endianness.Little, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutUnsignedShort(Endianness.Little, 256, 0);

            Assert.Equal(1, byteArraySegment[1]);
            Assert.Equal(0, byteArraySegment[0]);

            num = byteArraySegment.GetUnsignedShort(Endianness.Little, 0);

            Assert.Equal(256, num);
        }

        [Fact]
        public void UnsignedShortBigEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[2]);

            byteArraySegment.PutUnsignedShort(Endianness.Big, 1, 0);

            Assert.Equal(1, byteArraySegment[1]);
            Assert.Equal(0, byteArraySegment[0]);

            long num = byteArraySegment.GetUnsignedShort(Endianness.Big, 0);

            Assert.Equal(1, num);

            byteArraySegment.PutUnsignedShort(Endianness.Big, 256, 0);

            Assert.Equal(1, byteArraySegment[0]);
            Assert.Equal(0, byteArraySegment[1]);

            num = byteArraySegment.GetUnsignedShort(Endianness.Big, 0);

            Assert.Equal(256, num);
        }
    }
}
