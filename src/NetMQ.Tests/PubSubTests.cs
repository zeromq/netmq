using System.Threading;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class PubSubTests
    {
        [Test]
        public void TopicPubSub()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("A");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendMore("A").Send("Hello");

                CollectionAssert.AreEqual(
                    new[] {"A", "Hello"},
                    sub.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void SimplePubSub()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                // Send the topic only
                pub.Send("A");

                CollectionAssert.AreEqual(
                    new[] { "A" },
                    sub.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void NotSubscribed()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.Send("Hello");

                Assert.IsFalse(sub.TrySkipFrame());
            }
        }

        /// <summary>
        /// This test trying to reproduce issue #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Test]
        public void MultipleSubscriptions()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
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
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub1 = context.CreateSubscriberSocket())
            using (var sub2 = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub1.Connect("tcp://127.0.0.1:" + port);
                sub2.Connect("tcp://127.0.0.1:" + port);
                
                sub1.Subscribe("1");
                sub1.Subscribe("1&2");

                sub2.Subscribe("2");
                sub2.Subscribe("1&2");

                Thread.Sleep(500);

                pub.SendMore("1").Send("A");

                CollectionAssert.AreEqual(new[] { "1", "A" }, sub1.ReceiveMultipartStrings());
                Assert.IsFalse(sub2.TrySkipFrame());

                pub.SendMore("2").Send("B");
            
                Assert.IsFalse(sub1.TrySkipFrame());
                CollectionAssert.AreEqual(new[] { "2", "B" }, sub2.ReceiveMultipartStrings());

                pub.SendMore("1&2").Send("C");

                CollectionAssert.AreEqual(new[] { "1&2", "C" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "1&2", "C" }, sub2.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void MultiplePublishersAndSubscribersOnSameTopic()
        {
            using (var context = NetMQContext.Create())
            using (var pub1 = context.CreatePublisherSocket())
            using (var pub2 = context.CreatePublisherSocket())
            using (var sub1 = context.CreateSubscriberSocket())
            using (var sub2 = context.CreateSubscriberSocket())
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
                pub1.SendMore("A").Send("Hello from the first publisher");

                CollectionAssert.AreEqual(new[] { "A", "Hello from the first publisher" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "A", "Hello from the first publisher" }, sub2.ReceiveMultipartStrings());

                // Send from pub 2
                pub2.SendMore("A").Send("Hello from the second publisher");

                CollectionAssert.AreEqual(new[] { "A", "Hello from the second publisher" }, sub1.ReceiveMultipartStrings());
                CollectionAssert.AreEqual(new[] { "A", "Hello from the second publisher" }, sub2.ReceiveMultipartStrings());
            }
        }


        [Test]
        public void Unsubscribe()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                int port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.Subscribe("A");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendMore("A").Send("Hello");

                CollectionAssert.AreEqual(new[] { "A", "Hello" }, sub.ReceiveMultipartStrings());

                sub.Unsubscribe("A");

                Thread.Sleep(500);

                pub.SendMore("A").Send("Hello again");

                Assert.IsFalse(sub.TrySkipFrame());
            }
        }
    }
}
