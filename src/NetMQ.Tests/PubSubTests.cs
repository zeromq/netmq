using System.Linq;
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

                pub.SendMore("A");
                pub.Send("Hello");

                bool more;

                Assert.AreEqual("A", sub.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello", sub.ReceiveString(out more));
                Assert.False(more);
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

                pub.Send("Hello");

                bool more;

                string m = sub.ReceiveString(out more);

                Assert.AreEqual("Hello", m);
                Assert.False(more);
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
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

                bool more;

                sub.ReceiveString(true, out more);
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
        public void MultipleSubscribers()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            using (var sub2 = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("A");
                sub.Subscribe("AB");
                sub.Subscribe("B");
                sub.Subscribe("C");

                sub2.Connect("tcp://127.0.0.1:" + port);
                sub2.Subscribe("A");
                sub2.Subscribe("AB");
                sub2.Subscribe("C");

                Thread.Sleep(500);

                pub.SendMore("AB");
                pub.Send("1");

                string message = sub.ReceiveStringMessages().First();

                Assert.AreEqual("AB", message, "First subscriber is expected to receive the message");

                message = sub2.ReceiveStringMessages().First();

                Assert.AreEqual("AB", message, "Second subscriber is expected to receive the message");
            }
        }

        [Test]
        public void MultiplePublishers()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var pub2 = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            using (var sub2 = context.CreateSubscriberSocket())
            {
                int port = pub.BindRandomPort("tcp://127.0.0.1");
                int port2 = pub2.BindRandomPort("tcp://127.0.0.1");

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Connect("tcp://127.0.0.1:" + port2);

                sub2.Connect("tcp://127.0.0.1:" + port);
                sub2.Connect("tcp://127.0.0.1:" + port2);

                // should subscribe to both
                sub.Subscribe("A");
                sub2.Subscribe("A");

                Thread.Sleep(500);


                pub.SendMore("A");
                pub.Send("Hello from the first publisher");

                bool more;

                Assert.AreEqual("A", sub.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello from the first publisher", sub.ReceiveString(out more));
                Assert.False(more);


                pub2.SendMore("A");
                pub2.Send("Hello from the second publisher");

                Assert.AreEqual("A", sub.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello from the second publisher", sub.ReceiveString(out more));
                Assert.False(more);


                // same for sub 2

                Assert.AreEqual("A", sub2.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello from the first publisher", sub2.ReceiveString(out more));
                Assert.False(more);

                Assert.AreEqual("A", sub2.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello from the second publisher", sub2.ReceiveString(out more));
                Assert.False(more);
            }
        }


        [Test, ExpectedException(typeof(AgainException))]
        public void UnSubscribe()
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

                pub.SendMore("A");
                pub.Send("Hello");

                bool more;

                string m = sub.ReceiveString(out more);

                Assert.AreEqual("A", m);
                Assert.IsTrue(more);

                string m2 = sub.ReceiveString(out more);

                Assert.AreEqual("Hello", m2);
                Assert.False(more);

                sub.Unsubscribe("A");

                Thread.Sleep(500);

                pub.SendMore("A");
                pub.Send("Hello");

                sub.ReceiveString(true, out more);
            }
        }
    }
}
