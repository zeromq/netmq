using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NUnit.Framework;

namespace NetMQ.Tests
{
    internal class MockReceivingSocket : IReceivingSocket
    {
        private readonly Queue<byte[]> m_frames = new Queue<byte[]>();

        public TimeSpan LastTimeout { get; private set; }

        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            LastTimeout = timeout;

            if (m_frames.Count == 0)
                return false;

            byte[] bytes = m_frames.Dequeue();

            msg.InitGC(bytes, bytes.Length);

            if (m_frames.Count != 0)
                msg.SetFlags(MsgFlags.More);

            return true;
        }

        public void PushFrame([NotNull] byte[] frame)
        {
            m_frames.Enqueue(frame);
        }
    }

    [TestFixture]
    public class ReceivingSocketExtensionsTests
    {
        #region Test support

        private MockReceivingSocket m_socket;

        [SetUp]
        public void SetUp()
        {
            m_socket = new MockReceivingSocket();
        }

        [NotNull]
        private byte[] PushFrame([NotNull] string hello)
        {
            byte[] expected = Encoding.ASCII.GetBytes(hello);
            m_socket.PushFrame(expected);
            return expected;
        }

        #endregion

        #region ReceiveFrameBytes

        [Test]
        public void ReceiveFrameBytesSingleFrame()
        {
            var expected = PushFrame("Hello");

            byte[] actual = m_socket.ReceiveFrameBytes();

            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);

            // The buffer is copied into a new array
            Assert.AreNotSame(expected, actual);
        }

        [Test]
        public void ReceiveFrameBytesMultiFrame()
        {
            var expected1 = PushFrame("Hello");
            var expected2 = PushFrame("World");

            bool more;
            byte[] actual1 = m_socket.ReceiveFrameBytes(out more);

            Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);
            Assert.IsTrue(more);
            Assert.IsTrue(actual1.SequenceEqual(expected1));
            Assert.AreNotSame(expected1, actual1);

            byte[] actual2 = m_socket.ReceiveFrameBytes(out more);

            Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);
            Assert.IsFalse(more);
            Assert.IsTrue(actual2.SequenceEqual(expected2));
            Assert.AreNotSame(expected2, actual2);
        }

        #endregion

        #region TryReceiveFrameBytes

        [Test]
        public void TryReceiveFrameBytes()
        {
            var expected = PushFrame("Hello");

            byte[] actual;
            Assert.IsTrue(m_socket.TryReceiveFrameBytes(out actual));

            Assert.AreEqual(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreNotSame(expected, actual);

            Assert.IsFalse(m_socket.TryReceiveFrameBytes(out actual));

            Assert.AreEqual(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.IsNull(actual);
        }

        [Test]
        public void TryReceiveFrameBytesWithMore()
        {
            var expected1 = PushFrame("Hello");
            var expected2 = PushFrame("World");

            bool more;
            byte[] actual;
            Assert.IsTrue(m_socket.TryReceiveFrameBytes(out actual, out more));

            Assert.AreEqual(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.IsTrue(actual.SequenceEqual(expected1));
            Assert.IsTrue(more);
            Assert.AreNotSame(expected1, actual);

            Assert.IsTrue(m_socket.TryReceiveFrameBytes(out actual, out more));

            Assert.AreEqual(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.IsTrue(actual.SequenceEqual(expected2));
            Assert.IsFalse(more);
            Assert.AreNotSame(expected1, actual);

            Assert.IsFalse(m_socket.TryReceiveFrameBytes(out actual, out more));
        }

        #endregion

        #region ReceiveMultipartBytes

        [Test]
        public void ReceiveMultipartBytes()
        {
            var expected = PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMultipartBytes();

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(4, actual.Capacity);
            Assert.IsTrue(actual[0].SequenceEqual(expected));
            Assert.AreNotSame(expected, actual[0]);
        }

        [Test]
        public void ReceiveMultipartBytesWithExpectedFrameCount()
        {
            var expected = PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMultipartBytes(expectedFrameCount: 1);

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(1, actual.Capacity);
            Assert.IsTrue(actual[0].SequenceEqual(expected));
            Assert.AreNotSame(expected, actual[0]);
        }

        #endregion

        #region ReceiveMultipartStrings

        [Test]
        public void ReceiveMultipartStrings()
        {
            const string expected = "Hello";

            PushFrame(expected);

            List<string> actual = m_socket.ReceiveMultipartStrings();

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(4, actual.Capacity);
            Assert.AreEqual(expected, actual[0]);
            Assert.AreNotSame(expected, actual[0]);
        }

        [Test]
        public void ReceiveMultipartStringsWithExpectedFrameCount()
        {
            const string expected = "Hello";

            PushFrame(expected);

            List<string> actual = m_socket.ReceiveMultipartStrings(expectedFrameCount: 1);

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(1, actual.Capacity);
            Assert.AreEqual(expected, actual[0]);
            Assert.AreNotSame(expected, actual[0]);
        }

        #endregion
    }
}
