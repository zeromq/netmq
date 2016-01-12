using System;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class MsgTests
    {
        [SetUp]
        public void SetUp()
        {
            BufferPool.SetGCBufferPool();
        }

        [Test]
        public void Constructor()
        {
            var msg = new Msg();

            Assert.AreEqual(0, msg.Size);
            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.IsNull(msg.Data);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsFalse(msg.IsInitialised);
            Assert.Throws<NullReferenceException>(() => msg[0] = 1);
            Assert.Throws<FaultException>(msg.Close);
        }

        [Test]
        public void Flags()
        {
            var msg = new Msg();

            Assert.AreEqual(MsgFlags.None, msg.Flags);

            msg.SetFlags(MsgFlags.Identity);

            Assert.IsTrue(msg.IsIdentity);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsShared);
            Assert.AreEqual(MsgFlags.Identity, msg.Flags);

            msg.SetFlags(MsgFlags.More);

            Assert.IsTrue(msg.IsIdentity);
            Assert.IsTrue(msg.HasMore);
            Assert.IsFalse(msg.IsShared);
            Assert.AreEqual(MsgFlags.Identity | MsgFlags.More, msg.Flags);

            msg.SetFlags(MsgFlags.Shared);

            Assert.IsTrue(msg.IsIdentity);
            Assert.IsTrue(msg.HasMore);
            Assert.IsTrue(msg.IsShared);
            Assert.AreEqual(MsgFlags.Identity | MsgFlags.More | MsgFlags.Shared, msg.Flags);

            msg.SetFlags(MsgFlags.Identity);
            msg.SetFlags(MsgFlags.More);
            msg.SetFlags(MsgFlags.More);
            msg.SetFlags(MsgFlags.Shared);
            msg.SetFlags(MsgFlags.Shared);
            msg.SetFlags(MsgFlags.Shared);

            Assert.AreEqual(MsgFlags.Identity | MsgFlags.More | MsgFlags.Shared, msg.Flags);

            msg.ResetFlags(MsgFlags.Shared);

            Assert.IsTrue(msg.IsIdentity);
            Assert.IsTrue(msg.HasMore);
            Assert.IsFalse(msg.IsShared);
            Assert.AreEqual(MsgFlags.Identity | MsgFlags.More, msg.Flags);

            msg.ResetFlags(MsgFlags.More);

            Assert.IsTrue(msg.IsIdentity);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsShared);
            Assert.AreEqual(MsgFlags.Identity, msg.Flags);

            msg.ResetFlags(MsgFlags.Identity);

            Assert.IsFalse(msg.IsIdentity);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsShared);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
        }

        [Test]
        public void InitEmpty()
        {
            var msg = new Msg();
            msg.InitEmpty();

            Assert.AreEqual(0, msg.Size);
            Assert.AreEqual(MsgType.Empty, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.IsNull(msg.Data);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsTrue(msg.IsInitialised);

            msg.Close();

            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.IsNull(msg.Data);
        }

        [Test]
        public void InitDelimiter()
        {
            var msg = new Msg();
            msg.InitDelimiter();

            Assert.AreEqual(0, msg.Size);
            Assert.AreEqual(MsgType.Delimiter, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.IsNull(msg.Data);
            Assert.IsFalse(msg.HasMore);
            Assert.IsTrue(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsTrue(msg.IsInitialised);

            msg.Close();

            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.IsNull(msg.Data);
        }

        [Test]
        public void InitGC()
        {
            var msg = new Msg();
            var bytes = new byte[200];
            msg.InitGC(bytes, 100);

            Assert.AreEqual(100, msg.Size);
            Assert.AreEqual(MsgType.GC, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.AreSame(bytes, msg.Data);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsTrue(msg.IsInitialised);

            msg.Close();

            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.IsNull(msg.Data);
        }


        [Test]
        public void InitGCOffset()
        {
            var msg = new Msg();
            var bytes = new byte[200];
            msg.InitGC(bytes, 100, 50);

            Assert.AreEqual(50, msg.Size);
            Assert.AreEqual(MsgType.GC, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.AreSame(bytes, msg.Data);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsTrue(msg.IsInitialised);

            var src = new byte[100];
            for (int i = 50; i < 100; i++) {
                src[i] = (byte)i;
            }
            msg.Put(src[50]);
            msg.Put(src[51], 1);
            msg.Put(src, 52, 2, 48);
            for (int i = 0; i < 50; i++) {
                msg[i] = (byte)(i + 50);
            }

            msg.Close();

            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.IsNull(msg.Data);
        }

        [Test]
        public void InitPool()
        {
            var pool = new MockBufferPool();
            BufferPool.SetCustomBufferPool(pool);

            var msg = new Msg();

            Assert.AreEqual(0, pool.TakeCallCount);

            msg.InitPool(100);

            Assert.AreEqual(1, pool.TakeCallCount);
            Assert.AreEqual(100, pool.TakeSize[0]);

            Assert.AreEqual(100, msg.Size);
            Assert.AreEqual(MsgType.Pool, msg.MsgType);
            Assert.AreEqual(MsgFlags.None, msg.Flags);
            Assert.IsNotNull(msg.Data);
            Assert.AreEqual(100, msg.Data.Length);
            Assert.IsFalse(msg.HasMore);
            Assert.IsFalse(msg.IsDelimiter);
            Assert.IsFalse(msg.IsIdentity);
            Assert.IsTrue(msg.IsInitialised);

            Assert.AreEqual(0, pool.ReturnCallCount);

            var bytes = msg.Data;

            msg.Close();

            Assert.AreEqual(1, pool.ReturnCallCount);
            Assert.AreSame(bytes, pool.ReturnBuffer[0]);

            Assert.AreEqual(MsgType.Uninitialised, msg.MsgType);
            Assert.IsNull(msg.Data);
        }

        [Test, ExpectedException(typeof(FaultException), ExpectedMessage = "Cannot copy an uninitialised Msg.")]
        public void CopyUninitialisedThrows()
        {
            var msg = new Msg();

            Assert.IsFalse(msg.IsInitialised);

            var msgCopy = new Msg();
            msgCopy.Copy(ref msg);
        }

        [Test]
        public void CopyPooled()
        {
            var pool = new MockBufferPool();
            BufferPool.SetCustomBufferPool(pool);

            var msg = new Msg();
            msg.InitPool(100);

            Assert.IsFalse(msg.IsShared);

            var copy = new Msg();
            copy.Copy(ref msg);

            Assert.IsTrue(msg.IsShared);
            Assert.IsTrue(copy.IsShared);

            msg.Close();

            Assert.AreEqual(0, pool.ReturnCallCount);
            Assert.IsFalse(msg.IsInitialised);
            Assert.IsNull(msg.Data);

            copy.Close();

            Assert.AreEqual(1, pool.ReturnCallCount);
            Assert.IsFalse(copy.IsInitialised);
            Assert.IsNull(copy.Data);
        }
    }
}