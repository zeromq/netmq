using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.ReactiveExtensions.Tests
{
	[TestFixture]	
	public class UnitTest
	{
		[Test]
		public void Simplest_Test()
		{
			Console.WriteLine(TestContext.CurrentContext.Test.Name);

			CountdownEvent cd = new CountdownEvent(5);
			{
				var pubSub = new SubjectNetMQ<int>("tcp://127.0.0.1:56001");
				pubSub.Subscribe(o =>
					{
						Console.Write($"Test 1: {o}\n");
						cd.Signal();
					},
					ex =>
					{
						//Console.WriteLine($"Exception! {ex.Message}");
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
		public void PubSubShouldNotCrashIfNoThreadSleep()
		{
			using (var pub = new PublisherSocket())
			{
				using (var sub = new SubscriberSocket())
				{
					int port = pub.BindRandomPort("tcp://127.0.0.1");
					sub.Connect("tcp://127.0.0.1:" + port);

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
					Console.WriteLine($"Connected in {sw.ElapsedMilliseconds} ms.");
				}
			}
		}
	}
}
