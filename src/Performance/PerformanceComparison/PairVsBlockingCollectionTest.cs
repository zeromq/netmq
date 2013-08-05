using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.PerformanceComparison
{
	[TestFixture(Description = "Test compares the performance of the in-process communication using PAIR sockets and BlockingCollection (lock-free thread-safe collection")]
	public class PairVsBlockingCollectionTest
	{
		[Test(Description = "Comparison of PAIR sockets with BlockingCollection using a single thread and a send-receive pattern")]
		public void SubmitMessagesOnOneThreadInterleaved()
		{
			var payload = new byte[100]; //actual payload is not important since it's always a reference copy
			const int iterations = 1000000;

			BlockingCollectionSingleThread(payload, iterations);

			PairSocketsSingleThread(payload, iterations);
		}

		[Test(Description = "Comparison of PAIR sockets with BlockingCollection using 2 threads with one producer and one consumer")]
		public void SubmitMessagesOnDifferentThreads()
		{
			var payload = new byte[100]; //actual payload is not important since it's always a reference copy
			const int iterations = 1000000;

			BlockingCollectionDifferentThreads(payload, iterations);

			BlockingCollectionConstrainedDifferentThreads(payload, iterations);

			PairSocketsDifferentThread(payload, iterations);
		}

		[Test(Description = "Comparison of PAIR sockets with BlockingCollection using 2 threads with request-reply pattern")]
		public void AsyncCommunicationBetweenThreads()
		{
			var payload = new byte[100]; //actual payload is not important since it's always a reference copy
			const int iterations = 100000;

			BlockingCollectionCommunicationBetweenThreads(payload, iterations);

			PairSocketsCommunicationBetweenThreads(payload, iterations);
		}

		#region Submit and receive messages on one thread, send/receive interleaved

		private static void BlockingCollectionSingleThread(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var blockingCollection = new BlockingCollection<byte[]>())
			{
				for (int i = 0; i < iterations; i++)
				{
					blockingCollection.Add(payload);
					blockingCollection.Take();
				}
			}

			Trace.WriteLine(iterations + " messages using BlockingCollection on the same thread, interleaved send/receive: " + stopwatch.ElapsedMilliseconds);
		}

		private static void PairSocketsSingleThread(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var ctx = NetMQContext.Create())
			using (var pair1 = ctx.CreatePairSocket())
			using (var pair2 = ctx.CreatePairSocket())
			{
				pair1.Bind("inproc://test");
				pair2.Connect("inproc://test");

				for (int i = 0; i < iterations; i++)
				{
					pair1.Send(payload);
					pair2.Receive();
				}
			}

			Trace.WriteLine(iterations + " messages using PAIR sockets on the same thread, interleaved send/receive: " + stopwatch.ElapsedMilliseconds);
		}

		#endregion

		#region Submit messages on one thread, receive on another

		private static void BlockingCollectionDifferentThreads(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var blockingCollection = new BlockingCollection<byte[]>())
			{
				var task = Task.Factory.StartNew(() =>
				{
					for (int i = 0; i < iterations; i++) // ReSharper disable once AccessToDisposedClosure
						blockingCollection.Take();
				});

				for (int i = 0; i < iterations; i++)
					blockingCollection.Add(payload);

				Trace.WriteLine(iterations + " messages using BlockingCollection on different threads, finished pushing: " +
								stopwatch.ElapsedMilliseconds);

				task.Wait();
			}

			Trace.WriteLine(iterations + " messages using BlockingCollection on different threads: " + stopwatch.ElapsedMilliseconds);
		}

		private static void BlockingCollectionConstrainedDifferentThreads(byte[] payload, int iterations)
		{
			const int limitOnQueueSize = 100; //Setting a hard limit on the number of messages in a queue

			Stopwatch stopwatch = Stopwatch.StartNew();
			using (var blockingCollection = new BlockingCollection<byte[]>(limitOnQueueSize)) 
			{
				var task = Task.Factory.StartNew(() =>
				{
					for (int i = 0; i < iterations; i++) // ReSharper disable once AccessToDisposedClosure
						blockingCollection.Take();
				});

				for (int i = 0; i < iterations; i++)
					blockingCollection.Add(payload);

				Trace.WriteLine(iterations + " messages using BlockingCollection(constrained) sockets on different threads, finished pushing: " +
					stopwatch.ElapsedMilliseconds);

				task.Wait();
			}

			Trace.WriteLine(iterations + " messages using BlockingCollection(constrained) on different threads: " + stopwatch.ElapsedMilliseconds);
		}

		private static void PairSocketsDifferentThread(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var ctx = NetMQContext.Create())
			using (var pair1 = ctx.CreatePairSocket())
			{
				pair1.Bind("inproc://test");

				var task = Task.Factory.StartNew(() =>
					{
						// ReSharper disable once AccessToDisposedClosure
						using (var pair2 = ctx.CreatePairSocket())
						{
							pair2.Connect("inproc://test");
							for (int i = 0; i < iterations; i++)
								pair2.Receive();
						}
					});

				for (int i = 0; i < iterations; i++)
					pair1.Send(payload);

				Trace.WriteLine(iterations + " messages using PAIR sockets on different threads, finished pushing: " + stopwatch.ElapsedMilliseconds);
				task.Wait();
			}

			Trace.WriteLine(iterations + " messages using PAIR sockets on different threads: " + stopwatch.ElapsedMilliseconds);
		}

		#endregion

		#region Synchronous request/reply pattern on 2 threads (send request, wait for reply before sending next request)

		private void BlockingCollectionCommunicationBetweenThreads(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var requestCollection = new BlockingCollection<byte[]>())
			using (var replyCollection = new BlockingCollection<byte[]>())
			{
				var task = Task.Factory.StartNew(() =>
				{
					for (int i = 0; i < iterations; i++)
					{
						// ReSharper disable AccessToDisposedClosure
						var bytes = requestCollection.Take();
						replyCollection.Add(bytes);
						// ReSharper restore AccessToDisposedClosure
					}
				});

				for (int i = 0; i < iterations; i++)
				{
					requestCollection.Add(payload);
					replyCollection.Take();
				}

				Trace.WriteLine(iterations + " request-reply conversations using BlockingCollection on different threads: " + stopwatch.ElapsedMilliseconds);
				task.Wait();
			}
		}

		private void PairSocketsCommunicationBetweenThreads(byte[] payload, int iterations)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using (var ctx = NetMQContext.Create())
			using (var pair1 = ctx.CreatePairSocket())
			{
				pair1.Bind("inproc://test");

				var task = Task.Factory.StartNew(() =>
				{
					// ReSharper disable once AccessToDisposedClosure
					using (var pair2 = ctx.CreatePairSocket())
					{
						pair2.Connect("inproc://test");
						for (int i = 0; i < iterations; i++)
						{
							var bytes = pair2.Receive();
							pair2.Send(bytes);
						}
					}
				});

				for (int i = 0; i < iterations; i++)
				{
					pair1.Send(payload);
					pair1.Receive();
				}

				Trace.WriteLine(iterations + " request-reply conversations using PAIR sockets on different threads: " + stopwatch.ElapsedMilliseconds);
				task.Wait();
			}
		}

		#endregion
	}
}
