// See BufferPool.cs for explanation of USE_SERVICE_MODEL define.

// #define USE_SERVICE_MODEL
using System;
using Xunit;

namespace NetMQ.Tests
{
    public abstract class BufferPoolTests
    {
        protected int maxBufferSize = 2;
        protected long maxBufferPoolSize = 100L;
        protected IBufferPool pool;

        [Theory]
        [InlineData(500)]
        [InlineData(50000)]
        [InlineData(5000000)]
        [InlineData(500000000)]
        public void Rent(int size)
        {
            /* The pool sizes were chosen to be very small. It's clear they do
             * not constitute any bounds on the array sizes that can be
             * requested. BufferManagerBufferPool has the same behavior
             * though. */
            var array = pool.Take(size);
            Assert.Equal(size, array.Length);
        }
        
        // I was not able to provoke this to happen. Maybe with a long.
        // [Fact]
        // public void RentTooBigBuffer()
        // {
        //     Assert.ThrowsAny<Exception>(() => pool.Take(900000000));
        // }

        [Fact]
        public void Return() {
            var array = pool.Take(10);
            pool.Return(array);
        }

        [Fact]
        public void ReturnUnknown() {
            var array = new byte[100];
            Assert.Throws<ArgumentException>(() => pool.Return(array));
        }
    }

    public class ArrayBufferPoolTests : BufferPoolTests
    {
        public ArrayBufferPoolTests()
        {
            pool = new ArrayBufferPool(maxBufferPoolSize, maxBufferSize);
        }
    }

#if USE_SERVICE_MODEL
    public class BufferManagerBufferPoolTests : BufferPoolTests
    {
        public BufferManagerBufferPoolTests()
        {
            pool = new BufferManagerBufferPool(maxBufferPoolSize, maxBufferSize);
        }
    }
#endif
}
