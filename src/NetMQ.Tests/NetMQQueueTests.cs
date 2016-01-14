using System;
using System.Threading;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class NetMQQueueTests
    {
        [Test]
        public void EnqueueDequeue()
        {
            using (var queue = new NetMQQueue<int>())
            {
                queue.Enqueue(1);

                Assert.AreEqual(1, queue.Dequeue());
            }
        }

        [Test]
        public void TryDequeue()
        {
            using (var queue = new NetMQQueue<int>())
            {
                int result;
                Assert.IsFalse(queue.TryDequeue(out result, TimeSpan.FromMilliseconds(100)));

                queue.Enqueue(1);

                Assert.IsTrue(queue.TryDequeue(out result, TimeSpan.FromMilliseconds(100)));
                Assert.AreEqual(1, result);
            }
        }

        [Test]
        public void WithPoller()
        {
            using (var queue = new NetMQQueue<int>())
            using (var poller = new NetMQPoller { queue })
            {
                var manualResetEvent = new ManualResetEvent(false);

                queue.ReceiveReady += (sender, args) =>
                {
                    Assert.AreEqual(1, queue.Dequeue());
                    manualResetEvent.Set();
                };

                poller.RunAsync();

                Assert.IsFalse(manualResetEvent.WaitOne(100));
                queue.Enqueue(1);
                Assert.IsTrue(manualResetEvent.WaitOne(100));
            }
        }
    }
}
