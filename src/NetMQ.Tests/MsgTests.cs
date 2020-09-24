using System;
using Xunit;

namespace NetMQ.Tests
{
    public class MsgTests
    {
        public MsgTests()
        {
            BufferPool.SetGCBufferPool();
        }

        [Fact]
        public void Constructor()
        {
            var msg = new Msg();

            Assert.Equal(0, msg.Size);
            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.Null(msg.UnsafeData);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.False(msg.IsInitialised);
            Assert.ThrowsAny<Exception>(() => msg[0] = 1);
            Assert.Throws<FaultException>((Action)msg.Close);
        }

        [Fact]
        public void Flags()
        {
            var msg = new Msg();

            Assert.Equal(MsgFlags.None, msg.Flags);

            msg.SetFlags(MsgFlags.Identity);

            Assert.True(msg.IsIdentity);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsShared);
            Assert.Equal(MsgFlags.Identity, msg.Flags);

            msg.SetFlags(MsgFlags.More);

            Assert.True(msg.IsIdentity);
            Assert.True(msg.HasMore);
            Assert.False(msg.IsShared);
            Assert.Equal(MsgFlags.Identity | MsgFlags.More, msg.Flags);

            msg.SetFlags(MsgFlags.Shared);

            Assert.True(msg.IsIdentity);
            Assert.True(msg.HasMore);
            Assert.True(msg.IsShared);
            Assert.Equal(MsgFlags.Identity | MsgFlags.More | MsgFlags.Shared, msg.Flags);

            msg.SetFlags(MsgFlags.Identity);
            msg.SetFlags(MsgFlags.More);
            msg.SetFlags(MsgFlags.More);
            msg.SetFlags(MsgFlags.Shared);
            msg.SetFlags(MsgFlags.Shared);
            msg.SetFlags(MsgFlags.Shared);

            Assert.Equal(MsgFlags.Identity | MsgFlags.More | MsgFlags.Shared, msg.Flags);

            msg.ResetFlags(MsgFlags.Shared);

            Assert.True(msg.IsIdentity);
            Assert.True(msg.HasMore);
            Assert.False(msg.IsShared);
            Assert.Equal(MsgFlags.Identity | MsgFlags.More, msg.Flags);

            msg.ResetFlags(MsgFlags.More);

            Assert.True(msg.IsIdentity);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsShared);
            Assert.Equal(MsgFlags.Identity, msg.Flags);

            msg.ResetFlags(MsgFlags.Identity);

            Assert.False(msg.IsIdentity);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsShared);
            Assert.Equal(MsgFlags.None, msg.Flags);
        }

        [Fact]
        public void InitEmpty()
        {
            var msg = new Msg();
            msg.InitEmpty();

            Assert.Equal(0, msg.Size);
            Assert.Equal(MsgType.Empty, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.Null(msg.UnsafeData);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.True(msg.IsInitialised);

            msg.Close();

            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Null(msg.UnsafeData);
        }

        [Fact]
        public void InitDelimiter()
        {
            var msg = new Msg();
            msg.InitDelimiter();

            Assert.Equal(0, msg.Size);
            Assert.Equal(MsgType.Delimiter, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.Null(msg.UnsafeData);
            Assert.False(msg.HasMore);
            Assert.True(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.True(msg.IsInitialised);

            msg.Close();

            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Null(msg.UnsafeData);
        }

        [Fact]
        public void InitGC()
        {
            var msg = new Msg();
            var bytes = new byte[200];
            msg.InitGC(bytes, 100);

            Assert.Equal(100, msg.Size);
            Assert.Equal(MsgType.GC, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.Same(bytes, msg.UnsafeData);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.True(msg.IsInitialised);

            msg.Close();

            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Null(msg.UnsafeData);
        }


        [Fact]
        public void InitGCOffset()
        {
            var msg = new Msg();
            var bytes = new byte[200];
            msg.InitGC(bytes, 100, 50);

            Assert.Equal(50, msg.Size);
            Assert.Equal(MsgType.GC, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.Same(bytes, msg.UnsafeData);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.True(msg.IsInitialised);

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

            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Null(msg.UnsafeData);
        }

        [Fact]
        public void InitPool()
        {
            var pool = new MockBufferPool();
            BufferPool.SetCustomBufferPool(pool);
            
            var counterPool = new MockCounterPool();
            AtomicCounterPool.SetCustomCounterPool(counterPool);

            var msg = new Msg();

            Assert.Equal(0, pool.TakeCallCount);
            Assert.Equal(0, counterPool.TakeCallCount);

            msg.InitPool(100);

            Assert.Equal(1, pool.TakeCallCount);
            Assert.Equal(100, pool.TakeSize[0]);
            Assert.Equal(1, counterPool.TakeCallCount);

            Assert.Equal(100, msg.Size);
            Assert.Equal(MsgType.Pool, msg.MsgType);
            Assert.Equal(MsgFlags.None, msg.Flags);
            Assert.NotNull(msg.UnsafeData);
            Assert.Equal(100, msg.UnsafeData!.Length);
            Assert.False(msg.HasMore);
            Assert.False(msg.IsDelimiter);
            Assert.False(msg.IsIdentity);
            Assert.True(msg.IsInitialised);

            Assert.Equal(0, pool.ReturnCallCount);
            Assert.Equal(0, counterPool.ReturnCallCount);

            var bytes = msg.UnsafeData;

            msg.Close();

            Assert.Equal(1, pool.ReturnCallCount);
            Assert.Same(bytes, pool.ReturnBuffer[0]);

            Assert.Equal(1, counterPool.ReturnCallCount);

            Assert.Equal(MsgType.Uninitialised, msg.MsgType);
            Assert.Null(msg.UnsafeData);
        }

        [Fact]
        public void CopyUninitialisedThrows()
        {
            var msg = new Msg();

            Assert.False(msg.IsInitialised);

            var msgCopy = new Msg();

            var exception = Assert.Throws<FaultException>(() => msgCopy.Copy(ref msg));

            Assert.Equal("Cannot copy an uninitialised Msg.", exception.Message);
        }

        [Fact]
        public void CopyPooled()
        {
            var pool = new MockBufferPool();
            BufferPool.SetCustomBufferPool(pool); 
            
            var counterPool = new MockCounterPool();            
            AtomicCounterPool.SetCustomCounterPool(counterPool);

            var msg = new Msg();
            msg.InitPool(100);

            Assert.False(msg.IsShared);

            var copy = new Msg();
            copy.Copy(ref msg);

            Assert.True(msg.IsShared);
            Assert.True(copy.IsShared);

            msg.Close();

            Assert.Equal(0, pool.ReturnCallCount);
            Assert.Equal(1, counterPool.ReturnCallCount);
            Assert.False(msg.IsInitialised);
            Assert.Null(msg.UnsafeData);            

            copy.Close();

            Assert.Equal(1, pool.ReturnCallCount);
            Assert.Equal(2, counterPool.ReturnCallCount);
            Assert.False(copy.IsInitialised);
            Assert.Null(copy.UnsafeData);
        }

        [Fact]
        public void ForeachMsg()
        {
            byte[] buffer =new byte[5] {1,2,3,4,5};
            Msg msg = new Msg();
            msg.InitGC(buffer, 1, 3);

            int sum = 0;
            
            foreach (var b in msg)
            {
                sum += b;
            }
            
            Assert.Equal(2 + 3 + 4, sum);
        }
    }
}