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

            NetworkOrderBitsConverter.PutInt64(num, buffer);

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

            NetworkOrderBitsConverter.PutInt32(num, buffer);

            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt32(buffer));
        }

        [Test]
        public void TestInt16()
        {
            unchecked
            {
                RoundTripInt16(0x0102, 1, 2);
                RoundTripInt16((short)0xFFEE, 0xFF, 0xEE);
            }
        }

        private static void RoundTripInt16(short num, params byte[] bytes)
        {
            byte[] buffer = NetworkOrderBitsConverter.GetBytes(num);

            Assert.AreEqual(2, buffer.Length);
            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt16(buffer));

            NetworkOrderBitsConverter.PutInt16(num, buffer);

            CollectionAssert.AreEqual(bytes, buffer);

            Assert.AreEqual(num, NetworkOrderBitsConverter.ToInt16(buffer));
        }

//        [Test]
//        public void PutInt64Perf()
//        {
//            for (var j = 0; j < 10; j++)
//            {
//                var buffer = new byte[8];
//                const int loopCount = 1000*1000*100;
//                var sw = Stopwatch.StartNew();
//                for (var k = 0; k < loopCount; k++)
//                    NetworkOrderBitsConverter.PutInt64(0x12345678ABCDEF12L, buffer);
//                Console.Out.WriteLine("{0}", sw.Elapsed.TotalMilliseconds);
//            }
//        }
    }
}
