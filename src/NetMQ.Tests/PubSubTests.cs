using System;
using System.Diagnostics;
using System.Threading;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class PubSubTests
    {
        [Test]
        public void TopicPubSub()
        {            
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("A");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendMoreFrame("A").SendFrame("Hello");

                CollectionAssert.AreEqual(
                    new[] {"A", "Hello"},
                    sub.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void SimplePubSub()
        {            
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                // Send the topic only
                pub.SendFrame("A");

                CollectionAssert.AreEqual(
                    new[] { "A" },
                    sub.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void NotSubscribed()
        {            
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendFrame("Hello");

                Assert.IsFalse(sub.TrySkipFrame());
            }
        }

        /// <summary>
        /// This test trying to reproduce issue #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Test]
        public void MultipleSubscriptions()
        {            
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("C");
                sub.Subscribe("B");
                sub.Subscribe("A");
                sub.Subscribe("D");
                sub.Subscribe("E");

                Thread.Sleep(500);

                sub.Unsubscribe("C");
                sub.Unsubscribe("B");
                sub.Unsubscribe("A");
                sub.Unsubscribe("D");
                sub.Unsubscribe("E");

                Thread.Sleep(500);
            }
        }

        [Test]
        public void MultipleSubscribersOnDifferentTopics()
        {            
            using (var pub = new PublisherSocket())
            using (var sub1 = new SubscriberSocket())
            using (var sub2 = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub1.Connect("tcp://127.0.0.1:" + port);
                sub2.Connect("tcp://127.0.0.1:" + port);
                
                sub1.Subscribe("1");
                sub1.Subscribe("1&2");

                sub2.Subscribe("2");
                sub2.Subscribe("1&2");

                Thread.Sleep(500);

                pub.SendMoreFrame("1").SendFrame("A");

                CollectionAssert.AreEqual(new[] { "1", "A" }, sub1.ReceiveMultipartStrings());
                Assert.IsFalse(sub2.TrySkipFrame());

                pub.SendMoreFrame("2").SendFrame("B");
            
                Assert.IsFalse(sub1.TrySkipFrame());
                CollectionAssert.AreEqual(new[] { "2", "B" }, sub2.ReceiveMultipartStrings());

                pub.SendMoreFrame("1&2").SendFrame("C");

                CollectionAssert.AreEqual(new[] { "1&2", "C" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "1&2", "C" }, sub2.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void MultiplePublishersAndSubscribersOnSameTopic()
        {            
            using (var pub1 = new PublisherSocket())
            using (var pub2 = new PublisherSocket())
            using (var sub1 = new SubscriberSocket())
            using (var sub2 = new SubscriberSocket())
            {
                int port1 = pub1.BindRandomPort("tcp://127.0.0.1");
                int port2 = pub2.BindRandomPort("tcp://127.0.0.1");

                sub1.Connect("tcp://127.0.0.1:" + port1);
                sub1.Connect("tcp://127.0.0.1:" + port2);

                sub2.Connect("tcp://127.0.0.1:" + port1);
                sub2.Connect("tcp://127.0.0.1:" + port2);

                // should subscribe to both
                sub1.Subscribe("A");
                sub2.Subscribe("A");

	            Stopwatch sw = Stopwatch.StartNew();
				sub1.Poll(TimeSpan.FromMilliseconds(500));
				sub2.Poll(TimeSpan.FromMilliseconds(500));
				Console.Write($"Elapsed: {sw.ElapsedMilliseconds}.\n");
				//Thread.Sleep(500);

				// Send from pub 1
				pub1.SendMoreFrame("A").SendFrame("Hello from the first publisher");

                CollectionAssert.AreEqual(new[] { "A", "Hello from the first publisher" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "A", "Hello from the first publisher" }, sub2.ReceiveMultipartStrings());

                // Send from pub 2
                pub2.SendMoreFrame("A").SendFrame("Hello from the second publisher");

                CollectionAssert.AreEqual(new[] { "A", "Hello from the second publisher" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "A", "Hello from the second publisher" }, sub2.ReceiveMultipartStrings());
            }
        }


        [Test]
        public void Unsubscribe()
        {            
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                int port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.Subscribe("A");

	            sub.Poll(TimeSpan.FromMilliseconds(500));

                // let the subscriber connect to the publisher before sending a message
                //Thread.Sleep(500);

                pub.SendMoreFrame("A").SendFrame("Hello");

                CollectionAssert.AreEqual(new[] { "A", "Hello" }, sub.ReceiveMultipartStrings());

                sub.Unsubscribe("A");

                Thread.Sleep(500);

                pub.SendMoreFrame("A").SendFrame("Hello again");

                Assert.IsFalse(sub.TrySkipFrame());
            }
        }

	    [Test]
	    public void PubSubShouldNotCrashIfNoThreadSleep()
	    {
			Stopwatch sw = Stopwatch.StartNew();
			using (var pub = new PublisherSocket())
		    {
				pub.Options.SendHighWatermark = 1000;
				using (var sub = new SubscriberSocket())
			    {
				    int port = pub.BindRandomPort("tcp://127.0.0.1");
				    sub.Connect("tcp://127.0.0.1:" + port);

				    sub.Subscribe("*");

					for (int i = 0; i < 50; i++)
					{
						pub.SendMoreFrame("*").SendFrame("P"); ; // Ping.

						Console.Write("*");

						string topic;
						var gotTopic = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out topic);
						string ping;
						var gotPing = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(10), out ping);
						if (gotTopic == true)
						{
							Console.Write("\n");
							break;
						}
					}
				}
		    }
			Console.WriteLine($"Connected in {sw.ElapsedMilliseconds} ms.");
		}
    }
}
