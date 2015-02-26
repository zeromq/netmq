using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class NetworkOrderBitsConverterTests
    {
        [Test]
        public void TestInt32()
        {
            byte[] buffer =  NetworkOrderBitsConverter.GetBytes((long)1);

            Assert.AreEqual(buffer[7], 1);
            Assert.AreEqual(0, buffer[0]);

            long num = NetworkOrderBitsConverter.ToInt64(buffer);

            Assert.AreEqual(1, num);

            NetworkOrderBitsConverter.PutInt64(72057594037927936, buffer, 0);

            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(0, buffer[7]);

            num = NetworkOrderBitsConverter.ToInt64(buffer);

            Assert.AreEqual(72057594037927936, num);
        }

        [Test]
        public void TestInt64()
        {
            byte[] buffer = NetworkOrderBitsConverter.GetBytes(1);

            Assert.AreEqual(1, buffer[3]);
            Assert.AreEqual(0, buffer[0]);

            long num = NetworkOrderBitsConverter.ToInt32(buffer);

            Assert.AreEqual(1, num);

            NetworkOrderBitsConverter.PutInt32(16777216, buffer,0);

            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(0, buffer[3]);

            num = NetworkOrderBitsConverter.ToInt32(buffer);

            Assert.AreEqual(16777216, num);
        }
    }
}
