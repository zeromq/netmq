using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [Obsolete("Tests an obsolete type")]
    [TestFixture]
    public class NetMQSchedulerTest
    {
        [Test]
        public void OneTask()
        {
            bool triggered = false;

            using (var scheduler = new NetMQScheduler())
            {
                var task = new Task(() => { triggered = true; });
                task.Start(scheduler);
                task.Wait();

                Assert.IsTrue(triggered);
            }
        }

        [Test]
        public void ContinueWith()
        {
            var threadId1 = 0;
            var threadId2 = 1;

            var runCount1 = 0;
            var runCount2 = 0;

            using (var scheduler = new NetMQScheduler())
            {
                var task = new Task(() =>
                {
                    threadId1 = Thread.CurrentThread.ManagedThreadId;
                    runCount1++;
                });

                var task2 = task.ContinueWith(t =>
                {
                    threadId2 = Thread.CurrentThread.ManagedThreadId;
                    runCount2++;
                }, scheduler);

                task.Start(scheduler);
                task.Wait();
                task2.Wait();

                Assert.AreEqual(threadId1, threadId2);
                Assert.AreEqual(1, runCount1);
                Assert.AreEqual(1, runCount2);
            }
        }

        [Test]
        public void ExternalPoller()
        {
            var triggered = false;

            using (var poller = new Poller())
            using (var scheduler = new NetMQScheduler(poller))
            {
                poller.PollTillCancelledNonBlocking();

                var task = new Task(() => { triggered = true; });
                task.Start(scheduler);
                task.Wait();

                Assert.IsTrue(triggered);
            }
        }

        [Test]
        public void CanDisposeSchedulerWhenPollerExternalAndCancelled()
        {
            using (var poller = new Poller())
            using (var scheduler = new NetMQScheduler(poller))
            {
                poller.PollTillCancelledNonBlocking();

                var startedEvent = new ManualResetEvent(false);
                Task.Factory.StartNew(() => { startedEvent.Set(); }, CancellationToken.None, TaskCreationOptions.None, scheduler);

                startedEvent.WaitOne();

                poller.CancelAndJoin();
            }
        }

        [Test]
        public void TwoThreads()
        {
            var count1 = 0;
            var count2 = 0;

            var allTasks = new ConcurrentBag<Task>();

            using (var scheduler = new NetMQScheduler())
            {
                var t1 = Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var task = new Task(() => { count1++; });
                        allTasks.Add(task);
                        task.Start(scheduler);
                    }
                });

                var t2 = Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var task = new Task(() => { count2++; });
                        allTasks.Add(task);
                        task.Start(scheduler);
                    }
                });

                t1.Wait(100);
                t2.Wait(100);
                Task.WaitAll(allTasks.ToArray(), 100);

                Assert.AreEqual(100, count1);
                Assert.AreEqual(100, count2);
            }
        }
    }
}
