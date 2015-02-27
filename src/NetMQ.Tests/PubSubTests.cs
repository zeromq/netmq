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
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    var port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:" + port);
                        sub.Subscribe("A");

                        // let the subscrbier connect to the publisher before sending a message
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
                    }
                }
            }
        }

        [Test]
        public void SimplePubSub()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    var port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:" + port);
                        sub.Subscribe("");

                        // let the subscrbier connect to the publisher before sending a message
                        Thread.Sleep(500);

                        pub.Send("Hello");

                        bool more;

                        string m = sub.ReceiveString(out more);

                        Assert.AreEqual("Hello", m);
                        Assert.False(more);
                    }
                }
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
        public void NotSubscribed()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    var port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:" + port);

                        // let the subscrbier connect to the publisher before sending a message
                        Thread.Sleep(500);

                        pub.Send("Hello");

                        bool more;

                        string m = sub.ReceiveString(true, out more);
                    }
                }
            }
        }

        /// <summary>
        /// This test trying to reproduce bug #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Test]
        public void MultipleSubscriptions()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    var port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
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
            }
        }

        [Test]
        public void MultipleSubscribers()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    var port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    using (var sub2 = contex.CreateSubscriberSocket())
                    {
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
            }
        }

        [Test]
        public void MultiplePublishers() {
            using (NetMQContext contex = NetMQContext.Create()) {
                using (var pub = contex.CreatePublisherSocket())
                using (var pub2 = contex.CreatePublisherSocket()) {
                    int port = pub.BindRandomPort("tcp://127.0.0.1");
                    int port2 = pub2.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    using (var sub2 = contex.CreateSubscriberSocket()) {

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
                        string m = sub.ReceiveString(out more);

                        Assert.AreEqual("A", m);
                        Assert.IsTrue(more);

                        string m2 = sub.ReceiveString(out more);

                        Assert.AreEqual("Hello from the first publisher", m2);
                        Assert.False(more);

                        pub2.SendMore("A");
                        pub2.Send("Hello from the second publisher");

                        string m3 = sub.ReceiveString(out more);

                        Assert.AreEqual("A", m3);
                        Assert.IsTrue(more);

                        string m4 = sub.ReceiveString(out more);

                        Assert.AreEqual("Hello from the second publisher", m4);
                        Assert.False(more);


                        // same for sub 2

                        m = sub2.ReceiveString(out more);

                        Assert.AreEqual("A", m);
                        Assert.IsTrue(more);

                        m2 = sub2.ReceiveString(out more);

                        Assert.AreEqual("Hello from the first publisher", m2);
                        Assert.False(more);

                        m3 = sub2.ReceiveString(out more);

                        Assert.AreEqual("A", m3);
                        Assert.IsTrue(more);

                        m4 = sub2.ReceiveString(out more);

                        Assert.AreEqual("Hello from the second publisher", m4);
                        Assert.False(more);

                    }
                }
            }
        }


        [Test, ExpectedException(typeof(AgainException))]
        public void UnSubscribe()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var pub = contex.CreatePublisherSocket())
                {
                    int port = pub.BindRandomPort("tcp://127.0.0.1");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:" + port);
                        sub.Subscribe("A");

                        // let the subscrbier connect to the publisher before sending a message
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

                        string m3 = sub.ReceiveString(true, out more);
                    }
                }
            }
        }
    }
}
