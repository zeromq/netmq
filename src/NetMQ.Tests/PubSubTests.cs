using System;
using System.Collections.Generic;
using System.Threading;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class PubSubTests : IClassFixture<CleanupAfterFixture>
    {
        public PubSubTests() => NetMQConfig.Cleanup();

        [Fact]
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

                Assert.Equal(
                    new[] {"A", "Hello"},
                    sub.ReceiveMultipartStrings());
            }
        }

        [Fact]
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

                Assert.Equal(
                    new[] { "A" },
                    sub.ReceiveMultipartStrings());
            }
        }

        [Fact]
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

                Assert.False(sub.TrySkipFrame());
            }
        }

        /// <summary>
        /// This test trying to reproduce issue #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Fact]
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

        [Fact]
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

                Assert.Equal(new[] { "1", "A" }, sub1.ReceiveMultipartStrings());
                Assert.False(sub2.TrySkipFrame());

                pub.SendMoreFrame("2").SendFrame("B");

                Assert.False(sub1.TrySkipFrame());
                Assert.Equal(new[] { "2", "B" }, sub2.ReceiveMultipartStrings());

                pub.SendMoreFrame("1&2").SendFrame("C");

                Assert.Equal(new[] { "1&2", "C" }, sub1.ReceiveMultipartStrings());
                Assert.Equal(new[] { "1&2", "C" }, sub2.ReceiveMultipartStrings());
            }
        }

        [Fact]
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

                Thread.Sleep(500);

                // Send from pub 1
                pub1.SendMoreFrame("A").SendFrame("Hello from the first publisher");

                Assert.Equal(new[] { "A", "Hello from the first publisher" }, sub1.ReceiveMultipartStrings());
                Assert.Equal(new[] { "A", "Hello from the first publisher" }, sub2.ReceiveMultipartStrings());

                // Send from pub 2
                pub2.SendMoreFrame("A").SendFrame("Hello from the second publisher");

                Assert.Equal(new[] { "A", "Hello from the second publisher" }, sub1.ReceiveMultipartStrings());
                Assert.Equal(new[] { "A", "Hello from the second publisher" }, sub2.ReceiveMultipartStrings());
            }
        }

        [Fact]
        public void Unsubscribe()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                int port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.Subscribe("A");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendMoreFrame("A").SendFrame("Hello");

                Assert.Equal(new[] { "A", "Hello" }, sub.ReceiveMultipartStrings());

                sub.Unsubscribe("A");

                Thread.Sleep(500);

                pub.SendMoreFrame("A").SendFrame("Hello again");

                Assert.False(sub.TrySkipFrame());
            }
        }

        [Fact]
        public void ThroughXPubXSub()
        {
            using (var xpub = new XPublisherSocket())
            using (var xsub = new XSubscriberSocket())
            using (var proxyPoller = new NetMQPoller {xsub, xpub})
            {
                var xPubPort = (ushort)xpub.BindRandomPort("tcp://*");
                var xSubPort = (ushort)xsub.BindRandomPort("tcp://*");

                var proxy = new Proxy(xsub, xpub, poller: proxyPoller);
                proxy.Start();

                proxyPoller.RunAsync();

                using (var pub = new PublisherSocket())
                using (var sub = new SubscriberSocket())
                {
                    // Client 1
                    sub.Connect(string.Format("tcp://localhost:{0}", xPubPort));
                    pub.Connect(string.Format("tcp://localhost:{0}", xSubPort));

                    sub.Subscribe("A");

                    // Client 2
                    Thread.Sleep(500);
                    pub.SendMoreFrame("A").SendFrame("Hello");

                    var frames = new List<string>();
                    Assert.True(sub.TryReceiveMultipartStrings(TimeSpan.FromSeconds(1), ref frames));
                    Assert.Equal(
                        new[] { "A", "Hello" },
                        frames);
                }
            }
        }

        [Fact]
        public void ThroughXPubXSubWithReconnectingPublisher()
        {
            using (var xpub = new XPublisherSocket())
            using (var xsub = new XSubscriberSocket())
            using (var poller = new NetMQPoller {xsub, xpub})
            {
                var xPubPort = (ushort)xpub.BindRandomPort("tcp://*");
                var xSubPort = (ushort)xsub.BindRandomPort("tcp://*");

                var proxy = new Proxy(xsub, xpub, poller: poller);
                proxy.Start();

                poller.RunAsync();

                // long running subscriber
                using (var sub = new SubscriberSocket())
                {
                    sub.Connect(string.Format("tcp://localhost:{0}", xPubPort));
                    sub.Subscribe("A");

                    // publisher 1
                    using (var pub = new PublisherSocket())
                    {
                        pub.Connect(string.Format("tcp://localhost:{0}", xSubPort));
                        // give the publisher a chance to learn of the subscription
                        Thread.Sleep(100);
                        pub.SendMoreFrame("A").SendFrame("1");
                    }

                    // publisher 2
                    using (var pub = new PublisherSocket())
                    {
                        pub.Connect(string.Format("tcp://localhost:{0}", xSubPort));
                        // give the publisher a chance to learn of the subscription
                        Thread.Sleep(100);
                        pub.SendMoreFrame("A").SendFrame("2");
                    }

                    var frames = new List<string>();

                    Assert.True(sub.TryReceiveMultipartStrings(TimeSpan.FromSeconds(1), ref frames));
                    Assert.Equal(new[] { "A", "1" }, frames);

                    Assert.True(sub.TryReceiveMultipartStrings(TimeSpan.FromSeconds(1), ref frames));
                    Assert.Equal(new[] { "A", "2" }, frames);
                }
            }
        }
    }
}
