#if !NET35
using System;
using System.Threading;
using Xunit;

namespace NetMQ.Tests
{
    public class NetMQQueueTests : IClassFixture<CleanupAfterFixture>
    {
        public NetMQQueueTests() => NetMQConfig.Cleanup();

        [Fact]
        public void EnqueueDequeue()
        {
            using (var queue = new NetMQQueue<int>())
            {
                queue.Enqueue(1);

                Assert.Equal(1, queue.Dequeue());
            }
        }

        [Fact]
        public void TryDequeue()
        {
            using (var queue = new NetMQQueue<int>())
            {
                Assert.False(queue.TryDequeue(out int result, TimeSpan.FromMilliseconds(100)));

                queue.Enqueue(1);

                Assert.True(queue.TryDequeue(out result, TimeSpan.FromMilliseconds(100)));
                Assert.Equal(1, result);
            }
        }

        [Fact]
        public void WithPoller()
        {
            using (var queue = new NetMQQueue<int>())
            using (var poller = new NetMQPoller { queue })
            {
                var manualResetEvent = new ManualResetEvent(false);

                queue.ReceiveReady += (sender, args) =>
                {
                    Assert.Equal(1, queue.Dequeue());
                    manualResetEvent.Set();
                };

                poller.RunAsync();

                Assert.False(manualResetEvent.WaitOne(100));
                queue.Enqueue(1);
                Assert.True(manualResetEvent.WaitOne(100));
            }
        }
    }
}
#endif
