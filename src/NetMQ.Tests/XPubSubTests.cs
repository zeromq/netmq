using System;
using System.Linq;
using System.Threading;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class XPubSubTests
    {
        [Test]
        public void TopicPubSub()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Send(new byte[] { 1, (byte)'A' });

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                var msg = pub.Receive();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

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

        [Test]
        public void Census()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Options.ReceiveTimeout = TimeSpan.FromSeconds(2.0);

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Send("Message from subscriber");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                var txt = pub.ReceiveString();
                Assert.AreEqual("Message from subscriber", txt);

                sub.Send(new byte[] { });

                var msg = pub.Receive();
                Assert.True(msg.Length == 0);
            }
        }

        [Test]
        public void SimplePubSub()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Send(new byte[] { 1 });

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.Send("Hello");

                bool more;

                string m = sub.ReceiveString(out more);

                Assert.AreEqual("Hello", m);
                Assert.False(more);
            }
        }

        [Test, ExpectedException(typeof (AgainException))]
        public void NotSubscribed()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
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
        /// This test trying to reproduce bug #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Test]
        public void MultipleSubscriptions()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.Send(new byte[] { 1, (byte)'C' });
                sub.Send(new byte[] { 1, (byte)'B' });
                sub.Send(new byte[] { 1, (byte)'A' });
                sub.Send(new byte[] { 1, (byte)'D' });
                sub.Send(new byte[] { 1, (byte)'E' });

                Thread.Sleep(500);

                sub.Send(new byte[] { 0, (byte)'C' });
                sub.Send(new byte[] { 0, (byte)'B' });
                sub.Send(new byte[] { 0, (byte)'A' });
                sub.Send(new byte[] { 0, (byte)'D' });
                sub.Send(new byte[] { 0, (byte)'E' });

                Thread.Sleep(500);
            }
        }

        [Test]
        public void MultipleSubscribers()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            using (var sub2 = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Send(new byte[] { 1, (byte)'A' });
                sub.Send(new byte[] { 1, (byte)'A', (byte)'B' });
                sub.Send(new byte[] { 1, (byte)'B' });
                sub.Send(new byte[] { 1, (byte)'C' });

                sub2.Connect("tcp://127.0.0.1:" + port);
                sub2.Send(new byte[] { 1, (byte)'A' });
                sub2.Send(new byte[] { 1, (byte)'A', (byte)'B' });
                sub2.Send(new byte[] { 1, (byte)'C' });

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
            using (var pub = context.CreateXPublisherSocket())
            using (var pub2 = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            using (var sub2 = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                var port2 = pub2.BindRandomPort("tcp://127.0.0.1");

                // see comments below why verbose option is needed in this test
                pub.Options.XPubVerbose = true;
                pub2.Options.XPubVerbose = true;

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Connect("tcp://127.0.0.1:" + port2);

                sub2.Connect("tcp://127.0.0.1:" + port);
                sub2.Connect("tcp://127.0.0.1:" + port2);

                // should subscribe to both
                sub.Send(new byte[] { 1, (byte)'A' });
                sub2.Send(new byte[] { 1, (byte)'A' });

                Thread.Sleep(500);

                var msg = pub.ReceiveString();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

                var msg2 = pub2.Receive();
                Assert.AreEqual(2, msg2.Length);
                Assert.AreEqual(1, msg2[0]);
                Assert.AreEqual('A', msg2[1]);


                // Next two blocks (pub(2).Receive) will hang without XPub verbose option: 
                // sub and sub2 both have sent `.Send(new byte[] { 1, (byte)'A' });` messages
                // which are the same, so XPub will discard the second message for .Receive()
                // because it is normally used to pass topics upstream.
                // (un)subs are done in XPub.cs at line 150-175, quote:
                // >> If the subscription is not a duplicate, store it so that it can be
                // >> passed to used on next recv call.
                // For verbose:
                // >> If true, send all subscription messages upstream, not just unique ones

                // These options must be set before sub2.Send(new byte[] { 1, (byte)'A' });
                // pub.Options.XPubVerbose = true;
                // pub2.Options.XPubVerbose = true;
                // Note that resending sub2.Send(..) here wont help because XSub won't resent existing subs to XPub - quite sane behavior
                // Comment out the verbose options and the next 8 lines and the test will 
                // still pass, even with non-unique messages from subscribers (see the bottom of the test)

                msg = pub.ReceiveString();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

                msg2 = pub2.Receive();
                Assert.AreEqual(2, msg2.Length);
                Assert.AreEqual(1, msg2[0]);
                Assert.AreEqual('A', msg2[1]);


                pub.SendMore("A");
                pub.Send("Hello from the first publisher");

                bool more;
                string m = sub.ReceiveString(out more);
                Assert.AreEqual("A", m);
                Assert.IsTrue(more);
                string m2 = sub.ReceiveString(out more);
                Assert.AreEqual("Hello from the first publisher", m2);
                Assert.False(more);
                // this returns the result of the latest 
                // connect - address2, not the source of the message
                // This is documented here: http://api.zeromq.org/3-2:zmq-getsockopt
                //var ep = sub2.Options.LastEndpoint;
                //Assert.AreEqual(address, ep);

                // same for sub2
                m = sub2.ReceiveString(out more);
                Assert.AreEqual("A", m);
                Assert.IsTrue(more);
                m2 = sub2.ReceiveString(out more);
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
                var ep = sub2.Options.LastEndpoint;
                Assert.AreEqual("tcp://127.0.0.1:" + port2, ep);


                // same for sub2
                m3 = sub2.ReceiveString(out more);
                Assert.AreEqual("A", m3);
                Assert.IsTrue(more);
                m4 = sub2.ReceiveString(out more);
                Assert.AreEqual("Hello from the second publisher", m4);
                Assert.False(more);

                // send both to address and address2
                sub.Send("Message from subscriber");
                sub2.Send("Message from subscriber 2");

                var txt = pub.ReceiveString();
                Assert.AreEqual("Message from subscriber", txt);
                var txt2 = pub2.ReceiveString();
                Assert.AreEqual("Message from subscriber", txt2);

                // Does not hang even though is the same as above, but the first byte is not 1 or 0.
                // Won't hang even when messages are equal
                txt = pub.ReceiveString();
                Assert.AreEqual("Message from subscriber 2", txt);
                txt2 = pub2.ReceiveString();
                Assert.AreEqual("Message from subscriber 2", txt2);
            }
        }

        [Test, ExpectedException(typeof (AgainException))]
        public void UnSubscribe()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Send(new byte[] { 1, (byte)'A' });

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

                sub.Send(new byte[] { 0, (byte)'A' });

                Thread.Sleep(500);

                pub.SendMore("A");
                pub.Send("Hello");

                sub.ReceiveString(true, out more);
            }
        }

        [Test]
        public void Manual()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateXSubscriberSocket())
            {
                pub.Bind("inproc://manual");
                pub.Options.ManualPublisher = true;

                sub.Connect("inproc://manual");

                sub.Send(new byte[] { 1, (byte)'A' });
                var subscription = pub.Receive();

                Assert.AreEqual(subscription[1], (byte)'A');

                pub.Subscribe("B");
                pub.Send("A");
                pub.Send("B");

                var topic = sub.ReceiveString();

                Assert.AreEqual("B", topic);
            }
        }

        [Test]
        public void WelcomeMessage()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Bind("inproc://welcome");
                pub.SetWelcomeMessage("W");

                sub.Subscribe("W");
                sub.Connect("inproc://welcome");

                var subscription = pub.Receive();

                Assert.AreEqual(subscription[1], (byte)'W');

                var welcomeMessage = sub.ReceiveString();

                Assert.AreEqual("W", welcomeMessage);
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
        public void ClearWelcomeMessage()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreateXPublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Bind("inproc://welcome");
                pub.SetWelcomeMessage("W");
                pub.ClearWelcomeMessage();

                sub.Subscribe("W");
                sub.Connect("inproc://welcome");

                var subscription = pub.Receive();

                Assert.AreEqual(subscription[1], (byte)'W');

                sub.ReceiveString(SendReceiveOptions.DontWait);
            }
        }
    }
}
