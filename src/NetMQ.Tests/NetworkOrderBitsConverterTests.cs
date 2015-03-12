using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class NetworkOrderBitsConverterTests
    {
        [Test]
        public void TestInt64()
        {
            unchecked
            {
                RoundTripInt64(0x0102030405060708, 1, 2, 3, 4, 5, 6, 7, 8);
                RoundTripInt64((long)0xFFEEDDCCBBAA9988, 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88);
            }
        }

        private static void RoundTripInt64(long num, params byte[] bytes)
        {
            byte[] buffer = NetworkOrderBitsConverter.GetBytes(num);

            Assert.AreEqual(8, buffer.Length);
            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt64(buffer));

            NetworkOrderBitsConverter.PutInt64(num, buffer, 0);

            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt64(buffer));
        }

        [Test]
        public void TestInt32()
        {
            unchecked
            {
                RoundTripInt32(0x01020304, 1, 2, 3, 4);
                RoundTripInt32((int)0xFFEEDDCC, 0xFF, 0xEE, 0xDD, 0xCC);
            }
        }

        private static void RoundTripInt32(int num, params byte[] bytes)
        {
            byte[] buffer = NetworkOrderBitsConverter.GetBytes(num);

            Assert.AreEqual(4, buffer.Length);
            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt32(buffer));

            NetworkOrderBitsConverter.PutInt32(num, buffer, 0);

            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt32(buffer));
        }
    }
}