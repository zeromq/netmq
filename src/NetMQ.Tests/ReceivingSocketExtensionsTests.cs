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

        [Obsolete]
        public SendReceiveOptions LastOptions { get; private set; }

        [Obsolete("Use Receive(ref Msg) or TryReceive(ref Msg,TimeSpan) instead.")]
        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            LastOptions = options;

            byte[] bytes = m_frames.Dequeue();

            msg.InitGC(bytes, bytes.Length);

            if (m_frames.Count != 0)
                msg.SetFlags(MsgFlags.More);
        }

        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            throw new NotImplementedException();
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

        #region Receive()

        [Test, Obsolete]
        public void ReceiveByteArraySingleFrame()
        {
            var expected = PushFrame("Hello");

            byte[] actual = m_socket.Receive();

            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            // The buffer is copied into a new array
            Assert.AreNotSame(expected, actual);
        }

        [Test, Obsolete]
        public void ReceiveByteArrayMultiFrame()
        {
            var expected1 = PushFrame("Hello");
            var expected2 = PushFrame("World");

            byte[] actual1 = m_socket.Receive();

            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            byte[] actual2 = m_socket.Receive();

            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            Assert.IsTrue(actual1.SequenceEqual(expected1));
            Assert.IsTrue(actual2.SequenceEqual(expected2));

            Assert.AreNotSame(expected1, actual1);
            Assert.AreNotSame(expected2, actual2);
        }

        #endregion

        #region Receive(SendReceiveOptions)

        [Test, Obsolete]
        public void ReceiveByteArrayWithSendReceiveOptions()
        {
            var expected = PushFrame("Hello");

            byte[] actual = m_socket.Receive(SendReceiveOptions.DontWait);

            Assert.AreEqual(SendReceiveOptions.DontWait, m_socket.LastOptions);
            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreNotSame(expected, actual);
        }

        #endregion

        #region Receive(out hasMore)

        [Test, Obsolete]
        public void ReceiveByteArrayHasMoreSingleFrame()
        {
            var expected = PushFrame("Hello");

            bool hasMore;
            byte[] actual = m_socket.Receive(out hasMore);

            Assert.IsFalse(hasMore);
            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            // The buffer is copied into a new array
            Assert.AreNotSame(expected, actual);
        }

        [Test, Obsolete]
        public void ReceiveByteArrayHasMoreMultiFrame()
        {
            var expected1 = PushFrame("Hello");
            var expected2 = PushFrame("World");

            bool hasMore;
            byte[] actual1 = m_socket.Receive(out hasMore);

            Assert.IsTrue(hasMore);
            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            byte[] actual2 = m_socket.Receive(out hasMore);

            Assert.IsFalse(hasMore);
            Assert.AreEqual(SendReceiveOptions.None, m_socket.LastOptions);

            Assert.IsTrue(actual1.SequenceEqual(expected1));
            Assert.IsTrue(actual2.SequenceEqual(expected2));

            Assert.AreNotSame(expected1, actual1);
            Assert.AreNotSame(expected2, actual2);
        }

        #endregion

        #region Receive(SendReceiveOptions, out hasMore)

        [Test, Obsolete]
        public void ReceiveByteArraySendReceiveOptionsHasMoreSingleFrame()
        {
            var expected = PushFrame("Hello");

            bool hasMore;
            byte[] actual = m_socket.Receive(SendReceiveOptions.DontWait, out hasMore);

            Assert.IsFalse(hasMore);
            Assert.IsTrue(actual.SequenceEqual(expected));
            Assert.AreEqual(SendReceiveOptions.DontWait, m_socket.LastOptions);

            // The buffer is copied into a new array
            Assert.AreNotSame(expected, actual);
        }

        [Test, Obsolete]
        public void ReceiveByteArraySendReceiveOptionsHasMoreMultiFrame()
        {
            var expected1 = PushFrame("Hello");
            var expected2 = PushFrame("World");

            bool hasMore;
            byte[] actual1 = m_socket.Receive(SendReceiveOptions.DontWait, out hasMore);

            Assert.IsTrue(hasMore);
            Assert.AreEqual(SendReceiveOptions.DontWait, m_socket.LastOptions);

            byte[] actual2 = m_socket.Receive(SendReceiveOptions.DontWait, out hasMore);

            Assert.IsFalse(hasMore);
            Assert.AreEqual(SendReceiveOptions.DontWait, m_socket.LastOptions);

            Assert.IsTrue(actual1.SequenceEqual(expected1));
            Assert.IsTrue(actual2.SequenceEqual(expected2));

            Assert.AreNotSame(expected1, actual1);
            Assert.AreNotSame(expected2, actual2);
        }

        #endregion

        #region ReceiveMessages()

        [Test, Obsolete]
        public void ReceiveMessages()
        {
            var expected = PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMessages();

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(4, actual.Capacity);
            Assert.IsTrue(actual[0].SequenceEqual(expected));
            Assert.AreNotSame(expected, actual[0]);
        }

        [Test, Obsolete]
        public void ReceiveMessagesWithExpectedFrameCount()
        {
            var expected = PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMessages(expectedFrameCount: 1);

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(1, actual.Capacity);
            Assert.IsTrue(actual[0].SequenceEqual(expected));
            Assert.AreNotSame(expected, actual[0]);
        }

        #endregion

        #region ReceiveStringMessages()

        [Test, Obsolete]
        public void ReceiveStringMessages()
        {
            const string expected = "Hello";

            PushFrame(expected);

            List<string> actual = m_socket.ReceiveStringMessages();

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(4, actual.Capacity);
            Assert.AreEqual(expected, actual[0]);
            Assert.AreNotSame(expected, actual[0]);
        }

        [Test, Obsolete]
        public void ReceiveStringMessagesWithExpectedFrameCount()
        {
            const string expected = "Hello";

            PushFrame(expected);

            List<string> actual = m_socket.ReceiveStringMessages(expectedFrameCount: 1);

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(1, actual.Capacity);
            Assert.AreEqual(expected, actual[0]);
            Assert.AreNotSame(expected, actual[0]);
        }

        #endregion
    }
}
