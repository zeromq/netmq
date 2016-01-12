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
            using (NetMQQueue<int> queue = new NetMQQueue<int>())
            {
                queue.Enqueue(1);

                Assert.AreEqual(1, queue.Dequeue());
            }
        }

        [Test]
        public void TryDequeue()
        {            
            using (NetMQQueue<int> queue = new NetMQQueue<int>())
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
            using (NetMQQueue<int> queue = new NetMQQueue<int>())
            {
                Poller poller = new Poller();
                poller.AddSocket(queue);

                ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                queue.ReceiveReady += (sender, args) =>
                {
                    manualResetEvent.Set();
                };

                poller.PollTillCancelledNonBlocking();

                Assert.IsFalse(manualResetEvent.WaitOne(100));
                queue.Enqueue(1);
                Assert.IsTrue(manualResetEvent.WaitOne(100));

                poller.CancelAndJoin();
            }
        }
    }
}
