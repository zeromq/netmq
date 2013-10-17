using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class NetMQSchedulerTest
	{
		[Test]
		public void OneTask()
		{
			bool triggered = false;

			using (NetMQContext context = NetMQContext.Create())
			{
				using (NetMQScheduler scheduler = new NetMQScheduler(context))
				{
					Task task = new Task(() =>
					                     	{
					                     		triggered = true;
					                     	});
					task.Start(scheduler);
					task.Wait();

					Assert.IsTrue(triggered);
				}
			}
		}

		[Test]
		public void ContinueWith()
		{
			int threadId1 = 0;
			int threadId2 = 1;

			int runCount1 = 0;
			int runCount2 = 0;

			using (NetMQContext context = NetMQContext.Create())
			{
				using (NetMQScheduler scheduler = new NetMQScheduler(context))
				{
					Task task = new Task(() =>
					                     	{
					                     		threadId1 = Thread.CurrentThread.ManagedThreadId;
					                     		runCount1++;
					                     	});
					Task task2 = task.ContinueWith(t =>
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
		}

		[Test]
		public void ExternalPoller()
		{
			bool triggered = false;

			using (NetMQContext context = NetMQContext.Create())
			{
				Poller poller = new Poller();				

				using (NetMQScheduler scheduler = new NetMQScheduler(context, poller))
				{
					Task.Factory.StartNew(poller.Start);

					Task task = new Task(() =>
					{
						triggered = true;
					});
					task.Start(scheduler);
					task.Wait();

					Assert.IsTrue(triggered);
				}

				poller.Stop();
			}
		}

		[Test]
		public void TwoThreads()
		{
			int count1 = 0;
			int count2 = 0;

			ConcurrentBag<Task> allTasks = new ConcurrentBag<Task>(); 

			using (NetMQContext context = NetMQContext.Create())
			{
				using (NetMQScheduler scheduler = new NetMQScheduler(context))
				{
					Task t1 = Task.Factory.StartNew(() =>
					                                	{
					                                		for (int i = 0; i < 100; i++)
					                                		{
					                                			Task task = new Task(() =>
					                                			                     	{
					                                			                     		count1++;
					                                			                     	});
					                                			allTasks.Add(task);
					                                			task.Start(scheduler);

					                                		}
					                                	});

					Task t2 = Task.Factory.StartNew(() =>
					{
						for (int i = 0; i < 100; i++)
						{
							Task task = new Task(() =>
							{
								count2++;
							});
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
}
