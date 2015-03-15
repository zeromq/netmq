using NetMQ.Core.Transports;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ByteArraySegmentTests
    {
        [Test]
        public void LongLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[8]);

            byteArraySegment.PutLong(Endianness.Little, 1, 0);

            Assert.AreEqual(byteArraySegment[0], 1);
            Assert.AreEqual(0, byteArraySegment[7]);

            long num = byteArraySegment.GetLong(Endianness.Little, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutLong(Endianness.Little, 72057594037927936, 0);

            Assert.AreEqual(1, byteArraySegment[7]);
            Assert.AreEqual(0, byteArraySegment[0]);

            num = byteArraySegment.GetLong(Endianness.Little, 0);

            Assert.AreEqual(72057594037927936, num);
        }

        [Test]
        public void LongSmallEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[8]);

            byteArraySegment.PutLong(Endianness.Big, 1, 0);

            Assert.AreEqual(byteArraySegment[7], 1);
            Assert.AreEqual(0, byteArraySegment[0]);

            long num = byteArraySegment.GetLong(Endianness.Big, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutLong(Endianness.Big, 72057594037927936, 0);

            Assert.AreEqual(1, byteArraySegment[0]);
            Assert.AreEqual(0, byteArraySegment[7]);

            num = byteArraySegment.GetLong(Endianness.Big, 0);

            Assert.AreEqual(72057594037927936, num);
        }

        [Test]
        public void IntLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[4]);

            byteArraySegment.PutInteger(Endianness.Little, 1, 0);

            Assert.AreEqual(1, byteArraySegment[0]);
            Assert.AreEqual(0, byteArraySegment[3]);

            long num = byteArraySegment.GetInteger(Endianness.Little, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutInteger(Endianness.Little, 16777216, 0);

            Assert.AreEqual(1, byteArraySegment[3]);
            Assert.AreEqual(0, byteArraySegment[0]);

            num = byteArraySegment.GetInteger(Endianness.Little, 0);

            Assert.AreEqual(16777216, num);
        }

        [Test]
        public void IntBigEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[4]);

            byteArraySegment.PutInteger(Endianness.Big, 1, 0);

            Assert.AreEqual(1, byteArraySegment[3]);
            Assert.AreEqual(0, byteArraySegment[0]);

            long num = byteArraySegment.GetInteger(Endianness.Big, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutInteger(Endianness.Big, 16777216, 0);

            Assert.AreEqual(1, byteArraySegment[0]);
            Assert.AreEqual(0, byteArraySegment[3]);

            num = byteArraySegment.GetInteger(Endianness.Big, 0);

            Assert.AreEqual(16777216, num);
        }

        [Test]
        public void UnsignedShortLittleEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[2]);

            byteArraySegment.PutUnsignedShort(Endianness.Little, 1, 0);

            Assert.AreEqual(1, byteArraySegment[0]);
            Assert.AreEqual(0, byteArraySegment[1]);

            long num = byteArraySegment.GetUnsignedShort(Endianness.Little, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutUnsignedShort(Endianness.Little, 256, 0);

            Assert.AreEqual(1, byteArraySegment[1]);
            Assert.AreEqual(0, byteArraySegment[0]);

            num = byteArraySegment.GetUnsignedShort(Endianness.Little, 0);

            Assert.AreEqual(256, num);
        }

        [Test]
        public void UnsignedShortBigEndian()
        {
            ByteArraySegment byteArraySegment = new ByteArraySegment(new byte[2]);

            byteArraySegment.PutUnsignedShort(Endianness.Big, 1, 0);

            Assert.AreEqual(1, byteArraySegment[1]);
            Assert.AreEqual(0, byteArraySegment[0]);

            long num = byteArraySegment.GetUnsignedShort(Endianness.Big, 0);

            Assert.AreEqual(1, num);

            byteArraySegment.PutUnsignedShort(Endianness.Big, 256, 0);

            Assert.AreEqual(1, byteArraySegment[0]);
            Assert.AreEqual(0, byteArraySegment[1]);

            num = byteArraySegment.GetUnsignedShort(Endianness.Big, 0);

            Assert.AreEqual(256, num);
        }
    }
}
