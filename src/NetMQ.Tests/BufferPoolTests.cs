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
            var array = pool.Take(size);
            Assert.Equal(size, array.Length);
        }
        
        // This never happens.
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
            /* The sizes were chosen to be very small. It's not clear they
             * constitute any bounds on the array sizes that can be
             * requested. */
            pool = new ArrayBufferPool(maxBufferPoolSize, maxBufferSize);
        }
    }

    public class BufferManagerBufferPoolTests : BufferPoolTests
    {
        public BufferManagerBufferPoolTests()
        {
            /* The sizes were chosen to be very small. It's not clear they
             * constitute any bounds on the array sizes that can be
             * requested. */
            pool = new BufferManagerBufferPool(maxBufferPoolSize, maxBufferSize);
        }
    }
}
