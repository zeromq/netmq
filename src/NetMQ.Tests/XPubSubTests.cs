using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ.Tests
{
    [TestFixture]
    public class XPubSubTests
    {
        [Test]
        public void TopicPubSub()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send(new byte[] {1, (byte) 'A'});

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
            }
        }

        [Test]
        public void Census()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Options.ReceiveTimeout = TimeSpan.FromSeconds(2.0);

                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send("Message from subscriber");

                        // let the subscriber connect to the publisher before sending a message
                        Thread.Sleep(500);

                        var txt = pub.ReceiveString();
                        Assert.AreEqual("Message from subscriber", txt);

                        sub.Send(new byte[] {});

                        var msg = pub.Receive();
                        Assert.True(msg.Length == 0);

                    }
                }
            }
        }

        [Test]
        public void SimplePubSub()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send(new byte[] {1});

                        // let the subscriber connect to the publisher before sending a message
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

        [Test, ExpectedException(typeof (AgainException))]
        public void NotSubscribed()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");

                        // let the subscriber connect to the publisher before sending a message
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
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send(new byte[] {1, (byte) 'C'});
                        sub.Send(new byte[] {1, (byte) 'B'});
                        sub.Send(new byte[] {1, (byte) 'A'});
                        sub.Send(new byte[] {1, (byte) 'D'});
                        sub.Send(new byte[] {1, (byte) 'E'});

                        Thread.Sleep(500);

                        sub.Send(new byte[] {0, (byte) 'C'});
                        sub.Send(new byte[] {0, (byte) 'B'});
                        sub.Send(new byte[] {0, (byte) 'A'});
                        sub.Send(new byte[] {0, (byte) 'D'});
                        sub.Send(new byte[] {0, (byte) 'E'});

                        Thread.Sleep(500);
                    }
                }
            }
        }

        [Test]
        public void MultipleSubscribers()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    using (var sub2 = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send(new byte[] {1, (byte) 'A'});
                        sub.Send(new byte[] {1, (byte) 'A', (byte) 'B'});
                        sub.Send(new byte[] {1, (byte) 'B'});
                        sub.Send(new byte[] {1, (byte) 'C'});

                        sub2.Connect("tcp://127.0.0.1:5002");
                        sub2.Send(new byte[] {1, (byte) 'A'});
                        sub2.Send(new byte[] {1, (byte) 'A', (byte) 'B'});
                        sub2.Send(new byte[] {1, (byte) 'C'});

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

        [Test, ExpectedException(typeof (AgainException))]
        public void UnSubscribe()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("tcp://127.0.0.1:5002");

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("tcp://127.0.0.1:5002");
                        sub.Send(new byte[] {1, (byte) 'A'});

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

                        sub.Send(new byte[] {0, (byte) 'A'});

                        Thread.Sleep(500);

                        pub.SendMore("A");
                        pub.Send("Hello");

                        string m3 = sub.ReceiveString(true, out more);
                    }
                }
            }
        }

        [Test]
        public void Manual()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("inproc://manual");
                    pub.Options.ManualPublisher = true;

                    using (var sub = contex.CreateXSubscriberSocket())
                    {
                        sub.Connect("inproc://manual");

                        sub.Send(new byte[]{1, (byte)'A'});
                        var subscription = pub.Receive();

                        Assert.AreEqual(subscription[1], (byte)'A');

                        pub.Subscribe("B");
                        pub.Send("A");
                        pub.Send("B");

                        var topic = sub.ReceiveString();

                        Assert.AreEqual("B", topic);
                    }
                }
            }
        }

        [Test]
        public void WelcomeMessage()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("inproc://welcome");
                    pub.SetWelcomeMessage("W");

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Subscribe("W");
                        sub.Connect("inproc://welcome");
                        
                        var subscription = pub.Receive();

                        Assert.AreEqual(subscription[1], (byte)'W');
                        
                        var welcomeMessage = sub.ReceiveString();

                        Assert.AreEqual("W", welcomeMessage);
                    }
                }
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
        public void ClearWelcomeMessage()
        {
            using (var contex = new Factory().CreateContext())
            {
                using (var pub = contex.CreateXPublisherSocket())
                {
                    pub.Bind("inproc://welcome");
                    pub.SetWelcomeMessage("W");
                    pub.ClearWelcomeMessage();

                    using (var sub = contex.CreateSubscriberSocket())
                    {
                        sub.Subscribe("W");
                        sub.Connect("inproc://welcome");

                        var subscription = pub.Receive();

                        Assert.AreEqual(subscription[1], (byte)'W');
                        
                        sub.ReceiveString(SendReceiveOptions.DontWait);                        
                    }
                }
            }
        }
    }
}
