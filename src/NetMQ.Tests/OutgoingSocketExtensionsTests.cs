using System;
using Xunit;

namespace NetMQ.Tests
{
    internal delegate bool TrySendDelegate(ref Msg msg, TimeSpan timeout, bool more);

    internal class MockOutgoingSocket : IOutgoingSocket
    {
        private readonly TrySendDelegate m_action;

        public MockOutgoingSocket(TrySendDelegate action)
        {
            m_action = action;
        }

        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_action(ref msg, timeout, more);
        }
    }

    public class OutgoingSocketExtensionsTests
    {
        [Fact]
        public void SendMultipartBytesTest()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(1, msg.Data[0]);
                    Assert.True(more);
                    count++;
                }
                else
                {
                    Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(2, msg.Data[0]);
                    Assert.False(more);
                    count++;
                }

                return true;
            });

            socket.SendMultipartBytes(new byte[] { 1 }, new byte[] { 2 });
            Assert.Equal(2, count);
        }

        [Fact]
        public void TrySendMultipartBytesWithTimeoutTest()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.Equal(TimeSpan.FromSeconds(1), timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(1, msg.Data[0]);
                    Assert.True(more);
                    count++;
                }
                else
                {
                    Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(2, msg.Data[0]);
                    Assert.False(more);
                    count++;
                }

                return true;
            });

            Assert.True(socket.TrySendMultipartBytes(TimeSpan.FromSeconds(1), new byte[] { 1 }, new byte[] { 2 }));
            Assert.Equal(2, count);
        }

        [Fact]
        public void TrySendMultipartBytesWithTimeoutTestFailed()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {

                Assert.Equal(TimeSpan.FromSeconds(1), timeout);
                Assert.Equal(1, msg.Data.Length);
                Assert.Equal(1, msg.Data[0]);
                Assert.True(more);
                count++;

                return false;
            });

            Assert.False(socket.TrySendMultipartBytes(TimeSpan.FromSeconds(1), new byte[] { 1 }, new byte[] { 2 }));
            Assert.Equal(1, count);
        }

        [Fact]
        public void TrySendMultipartBytesTest()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.Equal(TimeSpan.FromSeconds(0), timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(1, msg.Data[0]);
                    Assert.True(more);
                    count++;
                }
                else
                {
                    Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(2, msg.Data[0]);
                    Assert.False(more);
                    count++;
                }

                return true;
            });

            Assert.True(socket.TrySendMultipartBytes(new byte[] { 1 }, new byte[] { 2 }));
            Assert.Equal(2, count);
        }

        [Fact]
        public void TrySendMultipartMessageTest()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.Equal(TimeSpan.FromSeconds(0), timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(1, msg.Data[0]);
                    Assert.True(more);
                    count++;
                }
                else
                {
                    Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.Equal(1, msg.Data.Length);
                    Assert.Equal(2, msg.Data[0]);
                    Assert.False(more);
                    count++;
                }

                return true;
            });

            var message = new NetMQMessage();
            message.Append(new byte[] {1});
            message.Append(new byte[] {2});

            Assert.True(socket.TrySendMultipartMessage(message));
            Assert.Equal(2, count);
        }

        [Fact]
        public void TrySendMultipartMessageFailed()
        {
            var count = 0;

            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(TimeSpan.FromSeconds(0), timeout);
                Assert.Equal(1, msg.Data.Length);
                Assert.Equal(1, msg.Data[0]);
                Assert.True(more);
                count++;

                return false;
            });

            var message = new NetMQMessage();
            message.Append(new byte[] { 1 });
            message.Append(new byte[] { 2 });

            Assert.False(socket.TrySendMultipartMessage(message));
            Assert.Equal(1, count);
        }

        [Fact]
        public void SendFrameEmpty()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.Equal(0, msg.Data.Length);
                Assert.False(more);
                return true;
            });

            socket.SendFrameEmpty();
        }

        [Fact]
        public void SendMoreFrameEmpty()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.Equal(0, msg.Data.Length);
                Assert.True(more);
                return true;
            });

            var returnedSocket = socket.SendMoreFrameEmpty();
            Assert.Equal(returnedSocket, socket);
        }

        [Fact]
        public void TrySendFrameEmpty()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(TimeSpan.Zero, timeout);
                Assert.Equal(0, msg.Data.Length);
                Assert.False(more);
                return true;
            });

            Assert.True(socket.TrySendFrameEmpty());
        }


        [Fact]
        public void TrySendFrameEmptyFailed()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(TimeSpan.Zero, timeout);
                Assert.Equal(0, msg.Data.Length);
                Assert.False(more);
                return false;
            });

            Assert.False(socket.TrySendFrameEmpty());
        }

        [Fact]
        public void SignalTest()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.Equal(8, msg.Data.Length);

                var value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.Equal(0x7766554433221100L, value);

                Assert.False(more);
                return true;
            });

            socket.SignalOK();
        }

        [Fact]
        public void TrySignalTest()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(TimeSpan.Zero, timeout);
                Assert.Equal(8, msg.Data.Length);

                var value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.Equal(0x7766554433221100L, value);

                Assert.False(more);
                return true;
            });

            Assert.True(socket.TrySignalOK());
        }

        [Fact]
        public void TrySignalFailedTest()
        {
            var socket = new MockOutgoingSocket((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.Equal(TimeSpan.Zero, timeout);
                Assert.Equal(8, msg.Data.Length);

                var value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.Equal(0x7766554433221100L, value);

                Assert.False(more);
                return false;
            });

            Assert.False(socket.TrySignalOK());
        }
    }
}
