using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;
// ReSharper disable ConvertClosureToMethodGroup

namespace NetMQ.ReactiveExtensions.Tests
{
	[TestFixture]	
	public class NetMQ_ReactiveExtensions_Tests
	{
		[Test]
		public void Simplest_Test()
		{
			Console.WriteLine(TestContext.CurrentContext.Test.Name);

			CountdownEvent cd = new CountdownEvent(5);
			{
				int freePort = TcpPortFree();

				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(o =>
					{
						Console.Write("Test 1: {0}\n", o);
						cd.Signal();
					},
					ex =>
					{
						Console.WriteLine("Exception! {0}", ex.Message);
					});

				pubSub.OnNext(38);
				pubSub.OnNext(39);
				pubSub.OnNext(40);
				pubSub.OnNext(41);
				pubSub.OnNext(42);
			}

			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void Initialize_Publisher_Then_Subscriber()
		{
			Console.WriteLine(TestContext.CurrentContext.Test.Name);

			CountdownEvent cd = new CountdownEvent(5);
			{
				int freePort = TcpPortFree();

				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);

				// Forces the publisher to be initialized. Subscriber not set up yet, so this message will never get
                // delivered to the subscriber, which is what is should do.
				pubSub.OnNext(1); 

				pubSub.Subscribe(o =>
				{
					Console.Write("Test 1: {0}\n", o);
					cd.Signal();
				},
					ex =>
					{
						Console.WriteLine("Exception! {0}", ex.Message);
					});

				pubSub.OnNext(38);
				pubSub.OnNext(39);
				pubSub.OnNext(40);
				pubSub.OnNext(41);
				pubSub.OnNext(42);
			}

			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void Simplest_Fanout_Sub()
		{
			CountdownEvent cd = new CountdownEvent(3);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write("PubTwoThreadFanoutSub1: {0}\n", o);
					cd.Signal();
				});
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write("PubTwoThreadFanoutSub2: {0}\n", o);
					cd.Signal();
				});
				pubSub.Subscribe(o =>
				{
					Assert.AreEqual(o, 42);
					Console.Write("PubTwoThreadFanoutSub3: {0}\n", o);
					cd.Signal();
				});

				pubSub.OnNext(42);
			}

			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void OnException_Should_Get_Passed_To_Subscribers()
		{
			CountdownEvent weAreDone = new CountdownEvent(1);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(
					o =>
					{
						// If this gets called more than max times, it will throw an exception as it is going through 0.
						Assert.Fail();
					},
					ex =>
					{
						Console.Write("Exception: {0}", ex.Message);
						Assert.True(ex.Message.Contains("passed"));
						weAreDone.Signal();
					},
					() =>
					{
						Assert.Fail();
					});

				pubSub.OnError(new Exception("passed"));
			}
			if (weAreDone.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public void OnCompleted_Should_Get_Passed_To_Subscribers()
		{
			CountdownEvent weAreDone = new CountdownEvent(1);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				pubSub.Subscribe(
					o =>
					{
						// If this gets called more than max times, it will throw an exception as it is going through 0.
						Console.Write("FAIL!");
						Assert.Fail();
					},
					ex =>
					{
						Console.Write("FAIL!");
						Assert.Fail();
					},
					() =>
					{
						Console.Write("Pass!");
						weAreDone.Signal();
					});

				pubSub.OnCompleted();
			}
			if (weAreDone.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}		

		[Test]
		public static void Disposing_Of_One_Does_Not_Dispose_Of_The_Other()
		{
			Console.WriteLine("Disposing of one subscriber should not dispose of the other.");

			int max = 1000;
			CountdownEvent cd = new CountdownEvent(max);
			{
				int freePort = TcpPortFree();
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);
				var d1 = pubSub.Subscribe(o =>
					{
						cd.Signal();
					});

				var d2 = pubSub.Subscribe(o =>
					{
						Assert.Fail();
					},
					ex =>
					{
						Console.WriteLine("Exception in subscriber thread.");
					});
				d2.Dispose();

				for (int i = 0; i < max; i++)
				{
					pubSub.OnNext(i);
				}
			}
			if (cd.Wait(TimeSpan.FromSeconds(10)) == false) // Blocks until _countdown.Signal has been called.
			{
				Assert.Fail("Timed out, this test should complete in 10 seconds.");
			}
		}

		[Test]
		public static void Speed_Test()
		{
			Stopwatch sw = new Stopwatch();
			{
				var max = 200 * 1000;

				CountdownEvent cd = new CountdownEvent(max);
				var receivedNum = 0;
				{

					Console.Write("Speed test with {0} messages:\n", max);

					int freePort = TcpPortFree();
					var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:" + freePort);

					pubSub.Subscribe(i =>
					{
						receivedNum++;
						cd.Signal();
						if (i % 10000 == 0)
						{
							//Console.Write("*");
						}
					});

					sw.Start();
					for (int i = 0; i < max; i++)
					{
						pubSub.OnNext(i);
					}
				}
				if (cd.Wait(TimeSpan.FromSeconds(15)) == false) // Blocks until _countdown.Signal has been called.
				{
					Assert.Fail("\nTimed out, this test should complete in 10 seconds. receivedNum={0}", receivedNum);
				}

				sw.Stop();
				Console.Write("\nElapsed time: {0} milliseconds ({1:0,000}/sec)\n", sw.ElapsedMilliseconds, (double)max / (double)sw.Elapsed.TotalSeconds);
				// On my machine, achieved >120,000 messages per second.
			}
		}

		[Test]
		public void PubSub_Should_NotCrash_IfNo_Thread_Sleep()
		{
			using (var pub = new PublisherSocket())
			{
				using (var sub = new SubscriberSocket())
				{
					int freePort = TcpPortFree();
					pub.Bind("tcp://127.0.0.1:" + freePort);
					sub.Connect("tcp://127.0.0.1:" + freePort);

					sub.Subscribe("*");

					Stopwatch sw = Stopwatch.StartNew();
					{
						for (int i = 0; i < 50; i++)
						{
							pub.SendFrame("*"); // Ping.

							Console.Write("*");
							string topic;
							var gotTopic = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out topic);
							string ping;
							var gotPing = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out ping);
							if (gotTopic == true)
							{
								Console.Write("\n");
								break;
							}
						}
					}
					Console.WriteLine("Connected in {0} ms.", sw.ElapsedMilliseconds);
				}
			}
		}

		/// <summary>
		/// Intent: Returns next free TCP/IP port. See
		/// http://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
		/// </summary>
		/// <threadSafe>Yes. Quote: "I successfully used this technique to get a free port. I too was concerned about
		/// race-conditions, with some other process sneaking in and grabbing the recently-detected-as-free port. So I
		/// wrote a test with a forced Sleep(100) between var port = FreeTcpPort() and starting an HttpListener on the
		/// free port. I then ran 8 identical processes hammering on this in a loop. I could never hit the race
		/// condition. My anecdotal evidence (Win 7) is that the OS apparently cycles through the range of ephemeral
		/// ports (a few thousand) before coming around again. So the above snippet should be just fine." </threadSafe>
		/// <returns>A free TCP/IP port.</returns>
		public static int TcpPortFree()
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			return port;
		}

	}
}
