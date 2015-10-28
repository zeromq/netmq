using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            using (NetMQContext context = NetMQContext.Create())
            using (NetMQQueue<int> queue = new NetMQQueue<int>(context))
            {
                queue.Enqueue(1);

                Assert.AreEqual(1, queue.Dequeue());
            }
        }

        [Test]
        public void TryDequeue()
        {
            using (NetMQContext context = NetMQContext.Create())
            using (NetMQQueue<int> queue = new NetMQQueue<int>(context))
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
            using (NetMQContext context = NetMQContext.Create())
            using (NetMQQueue<int> queue = new NetMQQueue<int>(context))
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
