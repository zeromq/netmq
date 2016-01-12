using System;
using NUnit.Framework;

namespace NetMQ.Tests
{
    internal delegate bool TrySendDelegate(ref Msg msg, TimeSpan timeout, bool more);

    internal class OutgoingSocketMock : IOutgoingSocket
    {
        private readonly TrySendDelegate m_action;

        public OutgoingSocketMock(TrySendDelegate action)
        {
            m_action = action;
        }

        [Obsolete("Use Send(ref Msg, bool) or TrySend(ref Msg,TimeSpan, bool) instead.")]
        public void Send(ref Msg msg, SendReceiveOptions options)
        {

        }

        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_action(ref msg, timeout, more);
        }
    }

    [TestFixture]
    public class OutgoingSocketExtensionsTests
    {
        [Test]
        public void SendMultipartBytesTest()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(1, msg.Data[0]);
                    Assert.IsTrue(more);
                    count++;
                }
                else
                {
                    Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(2, msg.Data[0]);
                    Assert.IsFalse(more);
                    count++;
                }

                return true;
            });

            socket.SendMultipartBytes(new byte[] { 1 }, new byte[] { 2 });
            Assert.AreEqual(2, count);
        }

        [Test]
        public void TrySendMultipartBytesWithTimeoutTest()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(1), timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(1, msg.Data[0]);
                    Assert.IsTrue(more);
                    count++;
                }
                else
                {
                    Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(2, msg.Data[0]);
                    Assert.IsFalse(more);
                    count++;
                }

                return true;
            });

            Assert.IsTrue(socket.TrySendMultipartBytes(TimeSpan.FromSeconds(1), new byte[] { 1 }, new byte[] { 2 }));
            Assert.AreEqual(2, count);
        }

        [Test]
        public void TrySendMultipartBytesWithTimeoutTestFailed()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {

                Assert.AreEqual(TimeSpan.FromSeconds(1), timeout);
                Assert.AreEqual(1, msg.Data.Length);
                Assert.AreEqual(1, msg.Data[0]);
                Assert.IsTrue(more);
                count++;

                return false;
            });

            Assert.IsFalse(socket.TrySendMultipartBytes(TimeSpan.FromSeconds(1), new byte[] { 1 }, new byte[] { 2 }));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void TrySendMultipartBytesTest()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(0), timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(1, msg.Data[0]);
                    Assert.IsTrue(more);
                    count++;
                }
                else
                {
                    Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(2, msg.Data[0]);
                    Assert.IsFalse(more);
                    count++;
                }

                return true;
            });

            Assert.IsTrue(socket.TrySendMultipartBytes(new byte[] { 1 }, new byte[] { 2 }));
            Assert.AreEqual(2, count);
        }

        [Test]
        public void TrySendMultipartMessageTest()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                if (count == 0)
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(0), timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(1, msg.Data[0]);
                    Assert.IsTrue(more);
                    count++;
                }
                else
                {
                    Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                    Assert.AreEqual(1, msg.Data.Length);
                    Assert.AreEqual(2, msg.Data[0]);
                    Assert.IsFalse(more);
                    count++;
                }

                return true;
            });

            NetMQMessage message = new NetMQMessage();
            message.Append(new byte[] {1});
            message.Append(new byte[] {2});

            Assert.IsTrue(socket.TrySendMultipartMessage(message));
            Assert.AreEqual(2, count);
        }

        [Test]
        public void TrySendMultipartMessageFailed()
        {
            int count = 0;

            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(TimeSpan.FromSeconds(0), timeout);
                Assert.AreEqual(1, msg.Data.Length);
                Assert.AreEqual(1, msg.Data[0]);
                Assert.IsTrue(more);
                count++;

                return false;
            });

            NetMQMessage message = new NetMQMessage();
            message.Append(new byte[] { 1 });
            message.Append(new byte[] { 2 });

            Assert.IsFalse(socket.TrySendMultipartMessage(message));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void SendFrameEmpty()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.AreEqual(0, msg.Data.Length);
                Assert.IsFalse(more);
                return true;
            });

            socket.SendFrameEmpty();
        }

        [Test]
        public void SendMoreFrameEmpty()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.AreEqual(0, msg.Data.Length);
                Assert.IsTrue(more);
                return true;
            });

            var returnedSocket = socket.SendMoreFrameEmpty();
            Assert.AreEqual(returnedSocket, socket);
        }

        [Test]
        public void TrySendFrameEmpty()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(TimeSpan.Zero, timeout);
                Assert.AreEqual(0, msg.Data.Length);
                Assert.IsFalse(more);
                return true;
            });

            Assert.IsTrue(socket.TrySendFrameEmpty());
        }


        [Test]
        public void TrySendFrameEmptyFailed()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(TimeSpan.Zero, timeout);
                Assert.AreEqual(0, msg.Data.Length);
                Assert.IsFalse(more);
                return false;
            });

            Assert.IsFalse(socket.TrySendFrameEmpty());
        }

        [Test]
        public void SignalTest()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(SendReceiveConstants.InfiniteTimeout, timeout);
                Assert.AreEqual(8, msg.Data.Length);

                long value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.AreEqual(0x7766554433221100L, value);

                Assert.IsFalse(more);
                return true;
            });

            socket.SignalOK();
        }

        [Test]
        public void TrySignalTest()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(TimeSpan.Zero, timeout);
                Assert.AreEqual(8, msg.Data.Length);

                long value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.AreEqual(0x7766554433221100L, value);

                Assert.IsFalse(more);
                return true;
            });

            Assert.IsTrue(socket.TrySignalOK());
        }

        [Test]
        public void TrySignalFailedTest()
        {
            OutgoingSocketMock socket = new OutgoingSocketMock((ref Msg msg, TimeSpan timeout, bool more) =>
            {
                Assert.AreEqual(TimeSpan.Zero, timeout);
                Assert.AreEqual(8, msg.Data.Length);

                long value = NetworkOrderBitsConverter.ToInt64(msg.Data);

                Assert.AreEqual(0x7766554433221100L, value);

                Assert.IsFalse(more);
                return false;
            });

            Assert.IsFalse(socket.TrySignalOK());
        }
    }
}
