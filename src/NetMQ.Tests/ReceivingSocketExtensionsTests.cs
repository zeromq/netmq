using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

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

            var bytes = m_frames.Dequeue();

            msg.InitGC(bytes, bytes.Length);

            if (m_frames.Count != 0)
                msg.SetFlags(MsgFlags.More);

            return true;
        }

        public byte[] PushFrame(string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            m_frames.Enqueue(bytes);
            return bytes;
        }
    }

    public class ReceivingSocketExtensionsTests
    {
        private readonly MockReceivingSocket m_socket = new MockReceivingSocket();

        #region ReceiveFrameBytes

        [Fact]
        public void ReceiveFrameBytesSingleFrame()
        {
            var expected = m_socket.PushFrame("Hello");

            byte[] actual = m_socket.ReceiveFrameBytes();

            Assert.True(actual.SequenceEqual(expected));
            Assert.Equal(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);

            // The buffer is copied into a new array
            Assert.NotSame(expected, actual);
        }

        [Fact]
        public void ReceiveFrameBytesMultiFrame()
        {
            var expected1 = m_socket.PushFrame("Hello");
            var expected2 = m_socket.PushFrame("World");

            byte[] actual1 = m_socket.ReceiveFrameBytes(out bool more);

            Assert.Equal(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);
            Assert.True(more);
            Assert.True(actual1.SequenceEqual(expected1));
            Assert.NotSame(expected1, actual1);

            byte[] actual2 = m_socket.ReceiveFrameBytes(out more);

            Assert.Equal(SendReceiveConstants.InfiniteTimeout, m_socket.LastTimeout);
            Assert.False(more);
            Assert.True(actual2.SequenceEqual(expected2));
            Assert.NotSame(expected2, actual2);
        }

        #endregion

        #region TryReceiveFrameBytes

        [Fact]
        public void TryReceiveFrameBytes()
        {
            var expected = m_socket.PushFrame("Hello");

            Assert.True(m_socket.TryReceiveFrameBytes(out byte[] actual));

            Assert.Equal(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.True(actual.SequenceEqual(expected));
            Assert.NotSame(expected, actual);

            Assert.False(m_socket.TryReceiveFrameBytes(out actual));

            Assert.Equal(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.Null(actual);
        }

        [Fact]
        public void TryReceiveFrameBytesWithMore()
        {
            var expected1 = m_socket.PushFrame("Hello");
            var expected2 = m_socket.PushFrame("World");

            Assert.True(m_socket.TryReceiveFrameBytes(out byte[] actual, out bool more));

            Assert.Equal(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.True(actual.SequenceEqual(expected1));
            Assert.True(more);
            Assert.NotSame(expected1, actual);

            Assert.True(m_socket.TryReceiveFrameBytes(out actual, out more));

            Assert.Equal(TimeSpan.Zero, m_socket.LastTimeout);
            Assert.True(actual.SequenceEqual(expected2));
            Assert.False(more);
            Assert.NotSame(expected1, actual);

            Assert.False(m_socket.TryReceiveFrameBytes(out actual, out more));
        }

        #endregion

        #region ReceiveMultipartBytes

        [Fact]
        public void ReceiveMultipartBytes()
        {
            var expected = m_socket.PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMultipartBytes();

            Assert.Equal(1, actual.Count);
            Assert.Equal(4, actual.Capacity);
            Assert.True(actual[0].SequenceEqual(expected));
            Assert.NotSame(expected, actual[0]);
        }

        [Fact]
        public void ReceiveMultipartBytesWithExpectedFrameCount()
        {
            var expected = m_socket.PushFrame("Hello");

            List<byte[]> actual = m_socket.ReceiveMultipartBytes(expectedFrameCount: 1);

            Assert.Equal(1, actual.Count);
            Assert.Equal(1, actual.Capacity);
            Assert.True(actual[0].SequenceEqual(expected));
            Assert.NotSame(expected, actual[0]);
        }

        #endregion

        #region ReceiveMultipartStrings

        [Fact]
        public void ReceiveMultipartStrings()
        {
            const string expected = "Hello";

            m_socket.PushFrame(expected);

            List<string> actual = m_socket.ReceiveMultipartStrings();

            Assert.Equal(1, actual.Count);
            Assert.Equal(4, actual.Capacity);
            Assert.Equal(expected, actual[0]);
            Assert.NotSame(expected, actual[0]);
        }

        [Fact]
        public void ReceiveMultipartStringsWithExpectedFrameCount()
        {
            const string expected = "Hello";

            m_socket.PushFrame(expected);

            List<string> actual = m_socket.ReceiveMultipartStrings(expectedFrameCount: 1);

            Assert.Equal(1, actual.Count);
            Assert.Equal(1, actual.Capacity);
            Assert.Equal(expected, actual[0]);
            Assert.NotSame(expected, actual[0]);
        }

        #endregion
    }
}
