using System.Linq;
using System.Threading;
using NUnit.Framework;
using NetMQ.Sockets;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    [TestFixture]
    public class XPubSubTests
    {
        [Test]
        public void TopicPubSub()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.SendFrame(new byte[] { 1, (byte)'A' });

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                var msg = pub.ReceiveFrameBytes();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

                pub.SendMoreFrame("A");
                pub.SendFrame("Hello");

                bool more;

                Assert.AreEqual("A", sub.ReceiveFrameString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello", sub.ReceiveFrameString(out more));
                Assert.False(more);
            }
        }

        [Test]
        public void Census()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.SendFrame("Message from subscriber");

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                var txt = pub.ReceiveFrameString();
                Assert.AreEqual("Message from subscriber", txt);

                sub.SendFrame(new byte[] { });

                var msg = pub.ReceiveFrameBytes();
                Assert.True(msg.Length == 0);
            }
        }

        [Test]
        public void SimplePubSub()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.SendFrame(new byte[] { 1 });

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendFrame("Hello");

                bool more;
                Assert.AreEqual("Hello", sub.ReceiveFrameString(out more));
                Assert.False(more);
            }
        }

        [Test]
        public void NotSubscribed()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
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
        /// This test trying to reproduce bug #45 NetMQ.zmq.Utils.Realloc broken!
        /// </summary>
        [Test]
        public void MultipleSubscriptions()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.SendFrame(new byte[] { 1, (byte)'C' });
                sub.SendFrame(new byte[] { 1, (byte)'B' });
                sub.SendFrame(new byte[] { 1, (byte)'A' });
                sub.SendFrame(new byte[] { 1, (byte)'D' });
                sub.SendFrame(new byte[] { 1, (byte)'E' });

                Thread.Sleep(500);

                sub.SendFrame(new byte[] { 0, (byte)'C' });
                sub.SendFrame(new byte[] { 0, (byte)'B' });
                sub.SendFrame(new byte[] { 0, (byte)'A' });
                sub.SendFrame(new byte[] { 0, (byte)'D' });
                sub.SendFrame(new byte[] { 0, (byte)'E' });

                Thread.Sleep(500);
            }
        }

        [Test]
        public void MultipleSubscribers()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            using (var sub2 = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub.Connect("tcp://127.0.0.1:" + port);
                sub.SendFrame(new byte[] { 1, (byte)'A' });
                sub.SendFrame(new byte[] { 1, (byte)'A', (byte)'B' });
                sub.SendFrame(new byte[] { 1, (byte)'B' });
                sub.SendFrame(new byte[] { 1, (byte)'C' });

                sub2.Connect("tcp://127.0.0.1:" + port);
                sub2.SendFrame(new byte[] { 1, (byte)'A' });
                sub2.SendFrame(new byte[] { 1, (byte)'A', (byte)'B' });
                sub2.SendFrame(new byte[] { 1, (byte)'C' });

                Thread.Sleep(500);

                pub.SendMoreFrame("AB");
                pub.SendFrame("1");

                Assert.AreEqual("AB", sub.ReceiveMultipartStrings().First(), "First subscriber is expected to receive the message");

                Assert.AreEqual("AB", sub2.ReceiveMultipartStrings().First(), "Second subscriber is expected to receive the message");
            }
        }

        [Test]
        public void MultiplePublishers()
        {
            using (var pub = new XPublisherSocket())
            using (var pub2 = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            using (var sub2 = new XSubscriberSocket())
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
                sub.SendFrame(new byte[] { 1, (byte)'A' });
                sub2.SendFrame(new byte[] { 1, (byte)'A' });

                Thread.Sleep(500);

                var msg = pub.ReceiveFrameString();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

                var msg2 = pub2.ReceiveFrameBytes();
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

                msg = pub.ReceiveFrameString();
                Assert.AreEqual(2, msg.Length);
                Assert.AreEqual(1, msg[0]);
                Assert.AreEqual('A', msg[1]);

                msg2 = pub2.ReceiveFrameBytes();
                Assert.AreEqual(2, msg2.Length);
                Assert.AreEqual(1, msg2[0]);
                Assert.AreEqual('A', msg2[1]);


                pub.SendMoreFrame("A");
                pub.SendFrame("Hello from the first publisher");

                bool more;
                Assert.AreEqual("A", sub.ReceiveFrameString(out more));
                Assert.IsTrue(more);
                Assert.AreEqual("Hello from the first publisher", sub.ReceiveFrameString(out more));
                Assert.False(more);
                // this returns the result of the latest
                // connect - address2, not the source of the message
                // This is documented here: http://api.zeromq.org/3-2:zmq-getsockopt
                //var ep = sub2.Options.LastEndpoint;
                //Assert.AreEqual(address, ep);

                // same for sub2
                Assert.AreEqual("A", sub2.ReceiveFrameString(out more));
                Assert.IsTrue(more);
                Assert.AreEqual("Hello from the first publisher", sub2.ReceiveFrameString(out more));
                Assert.False(more);


                pub2.SendMoreFrame("A");
                pub2.SendFrame("Hello from the second publisher");

                Assert.AreEqual("A", sub.ReceiveFrameString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello from the second publisher", sub.ReceiveFrameString(out more));
                Assert.False(more);
                Assert.AreEqual("tcp://127.0.0.1:" + port2, sub2.Options.LastEndpoint);


                // same for sub2
                Assert.AreEqual("A", sub2.ReceiveFrameString(out more));
                Assert.IsTrue(more);
                Assert.AreEqual("Hello from the second publisher", sub2.ReceiveFrameString(out more));
                Assert.False(more);

                // send both to address and address2
                sub.SendFrame("Message from subscriber");
                sub2.SendFrame("Message from subscriber 2");

                Assert.AreEqual("Message from subscriber", pub.ReceiveFrameString());
                Assert.AreEqual("Message from subscriber", pub2.ReceiveFrameString());

                // Does not hang even though is the same as above, but the first byte is not 1 or 0.
                // Won't hang even when messages are equal
                Assert.AreEqual("Message from subscriber 2", pub.ReceiveFrameString());
                Assert.AreEqual("Message from subscriber 2", pub2.ReceiveFrameString());
            }
        }

        [Test]
        public void Unsubscribe()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.SendFrame(new byte[] { 1, (byte)'A' });

                // let the subscriber connect to the publisher before sending a message
                Thread.Sleep(500);

                pub.SendMoreFrame("A");
                pub.SendFrame("Hello");

                bool more;

                Assert.AreEqual("A", sub.ReceiveFrameString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Hello", sub.ReceiveFrameString(out more));
                Assert.False(more);

                sub.SendFrame(new byte[] { 0, (byte)'A' });

                Thread.Sleep(500);

                pub.SendMoreFrame("A");
                pub.SendFrame("Hello");

                string str;
                Assert.IsFalse(sub.TryReceiveFrameString(out str));
            }
        }

        [Test]
        public void Manual()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new XSubscriberSocket())
            {
                pub.Bind("inproc://manual");
                pub.Options.ManualPublisher = true;

                sub.Connect("inproc://manual");

                sub.SendFrame(new byte[] { 1, (byte)'A' });
                var subscription = pub.ReceiveFrameBytes();

                Assert.AreEqual(subscription[1], (byte)'A');

                pub.Subscribe("B");
                pub.SendFrame("A");
                pub.SendFrame("B");

                Assert.AreEqual("B", sub.ReceiveFrameString());
            }
        }

        [Test]
        public void WelcomeMessage()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Bind("inproc://welcome");
                pub.SetWelcomeMessage("W");

                sub.Subscribe("W");
                sub.Connect("inproc://welcome");

                var subscription = pub.ReceiveFrameBytes();

                Assert.AreEqual(subscription[1], (byte)'W');

                Assert.AreEqual("W", sub.ReceiveFrameString());
            }
        }

        [Test]
        public void ClearWelcomeMessage()
        {
            using (var pub = new XPublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Bind("inproc://welcome");
                pub.SetWelcomeMessage("W");
                pub.ClearWelcomeMessage();

                sub.Subscribe("W");
                sub.Connect("inproc://welcome");

                var subscription = pub.ReceiveFrameBytes();

                Assert.AreEqual(subscription[1], (byte)'W');

                Assert.IsFalse(sub.TrySkipFrame());
            }
        }

        [Test]
        public void BroadcastEnabled()
        {
            using (var pub = new XPublisherSocket())
            using (var sub1 = new XSubscriberSocket())
            using (var sub2 = new XSubscriberSocket())
            {
                pub.Bind("inproc://manual");
                pub.Options.XPubBroadcast = true;

                sub1.Connect("inproc://manual");
                sub2.Connect("inproc://manual");

                sub1.SendFrame(new byte[] {1, (byte) 'A'});
                sub2.SendFrame(new byte[] {1, (byte) 'A'});

                var payload = new[] {(byte) 42};

                var msg = new Msg();
                msg.InitEmpty();

                // add prefix 2 to the topic, this indicates a broadcast message and it will not be sent to sub1
                sub1.SendFrame(new byte[] {2, (byte) 'A'}, true);
                sub1.SendFrame(new byte[] {(byte) 42});
                var subscription = pub.ReceiveFrameBytes();
                var topic = pub.ReceiveFrameBytes();
                var message = pub.ReceiveFrameBytes();

                Assert.AreEqual(2, topic[0]);
                // we must skip the first byte if we have detected a broadcast message
                // the sender of this message is already marked for exclusion
                // but the match logic in Send should work with normal topic.
                topic = topic.Skip(1).ToArray();

                pub.SendFrame(topic, true);
                pub.SendFrame(message);
                var broadcast2 = sub2.ReceiveFrameBytes();
                Assert.IsTrue(broadcast2[0] == 65);
                broadcast2 = sub2.ReceiveFrameBytes();
                Assert.IsTrue(broadcast2.SequenceEqual(payload));
                // this message SHOULD NOT be resent to sub1
                var received = sub1.TryReceive(ref msg, System.TimeSpan.FromMilliseconds(500));
                Assert.IsFalse(received);
            }
        }

        [Test]
        public void BroadcastDisabled()
        {
            using (var pub = new XPublisherSocket())
            using (var sub1 = new XSubscriberSocket())
            using (var sub2 = new XSubscriberSocket())
            {
                pub.Bind("inproc://manual");
                pub.Options.XPubBroadcast = false;

                sub1.Connect("inproc://manual");
                sub2.Connect("inproc://manual");

                Thread.Sleep(50);

                sub1.SendFrame(new byte[] { 1, (byte)'A' });
                sub2.SendFrame(new byte[] { 1, (byte)'A' });

                var payload = new[] { (byte)42 };

                sub1.SendFrame(new byte[] { (byte)'A' }, true);
                sub1.SendFrame(new byte[] { (byte)42 });
                var subscription = pub.ReceiveFrameBytes();
                var topic = pub.ReceiveFrameBytes();
                var message = pub.ReceiveFrameBytes();

                pub.SendFrame(topic, true);
                pub.SendFrame(message);
                var broadcast2 = sub2.ReceiveFrameBytes();
                Assert.IsTrue(broadcast2[0] == 65);
                broadcast2 = sub2.ReceiveFrameBytes();
                Assert.IsTrue(broadcast2.SequenceEqual(payload));
                // sub1 should receive a message normally
                var broadcast1 = sub1.ReceiveFrameBytes();
                Assert.IsTrue(broadcast1[0] == 65);
                broadcast1 = sub1.ReceiveFrameBytes();
                Assert.IsTrue(broadcast1.SequenceEqual(payload));
            }
        }


        [Test]
        public void CouldTrackSubscriberIdentityInXPubSocket() {
            using (var pub = new XPublisherSocket())
            using (var sub1 = new XSubscriberSocket())
            using (var sub2 = new XSubscriberSocket()) {
                pub.Bind("inproc://manual");
                pub.Options.ManualPublisher = true;

                sub1.Connect("inproc://manual");
                sub2.Connect("inproc://manual");

                Thread.Sleep(50);

                sub1.SendFrame(new byte[] { 1, (byte)'A' });

                var identity1 = new byte[] { 1 };
                var identity2 = new byte[] { 2 };

                sub1.SendFrame(new byte[] { (byte)'A' }, true);
                sub1.SendFrame(new byte[] { (byte)42 });
                var subscription = pub.ReceiveFrameBytes();
                // NB Identity must be set before pub.Subscribe/Unsubscribe/Send, because these operations clear a private field with last subscriber
                
                // set identity to sub1
                pub.Options.Identity = identity1;

                Assert.IsTrue(identity1.SequenceEqual(pub.Options.Identity), "Cannot read identity that was just set");

                pub.Subscribe(subscription);

                Assert.IsTrue(identity1.SequenceEqual(pub.Options.Identity), "Identity must be kept after Subscribe/Unsubscribe/Send operations (which clear m_lastPipe)");

                var topic = pub.ReceiveFrameBytes();
                var message = pub.ReceiveFrameBytes();

                sub2.SendFrame(new byte[] { 1, (byte)'A' });
                sub2.SendFrame(new byte[] { (byte)'A' }, true);
                sub2.SendFrame(new byte[] { (byte)43 });

                subscription = pub.ReceiveFrameBytes();
                // Id of sub2 is not set yet
                Assert.IsNull(pub.Options.Identity);
                pub.Options.Identity = identity2;
                Assert.IsTrue(identity2.SequenceEqual(pub.Options.Identity), "Cannot read identity that was just set");

                pub.Subscribe(subscription);

                Assert.IsTrue(identity2.SequenceEqual(pub.Options.Identity), "Identity must be kept after Subscribe/Unsubscribe/Send operations (which clear m_lastPipe)");

                topic = pub.ReceiveFrameBytes();
                message = pub.ReceiveFrameBytes();
                
                sub1.SendFrame(new byte[] { (byte)'A' }, true);
                sub1.SendFrame(new byte[] { (byte)44 });
                topic = pub.ReceiveFrameBytes();
                message = pub.ReceiveFrameBytes();
                Assert.IsTrue(identity1.SequenceEqual(pub.Options.Identity), "Identity option must be set to the identity of sub1 here");


                sub2.SendFrame(new byte[] { (byte)'A' }, true);
                sub2.SendFrame(new byte[] { (byte)45 });
                topic = pub.ReceiveFrameBytes();
                message = pub.ReceiveFrameBytes();
                Assert.IsTrue(identity2.SequenceEqual(pub.Options.Identity), "Identity option must be set to the identity of sub2 here");
            }
        }
    }
}
