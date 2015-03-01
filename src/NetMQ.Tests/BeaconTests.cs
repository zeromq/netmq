using System;
using System.Threading;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class BeaconTests
    {
        [Test]
        public void SimplePublishSubscribe()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);
                Console.WriteLine(speaker.Hostname);

                speaker.Publish("Hello");

                listener.Configure(9999);
                listener.Subscribe("H");

                string peerName;
                string message = listener.ReceiveString(out peerName);

                Console.WriteLine(peerName);

                Assert.AreEqual("Hello", message);
            }
        }

        [Test]
        public void Silence()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);

                listener.Configure(9999);
                listener.Subscribe("H");

                // this should send one broadcast message and stop
                speaker.Publish("Hello");
                Thread.Sleep(10);
                speaker.Silence();

                string peerName;
                string message = listener.ReceiveString(out peerName);

                Assert.AreEqual("Hello", message);

                ISocketPollable socket = listener;
                socket.Socket.Options.ReceiveTimeout = TimeSpan.FromSeconds(2);

                Assert.Throws<AgainException>(() => { message = listener.ReceiveString(out peerName); });
            }
        }

        [Test]
        public void Unsubscribe()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);

                listener.Configure(9999);
                listener.Subscribe("H");

                // this should send one broadcast message and stop
                speaker.Publish("Hello");

                string peerName;
                string message = listener.ReceiveString(out peerName);

                listener.Unsubscribe();

                Assert.AreEqual("Hello", message);

                ISocketPollable socket = listener;
                socket.Socket.Options.ReceiveTimeout = TimeSpan.FromSeconds(2);

                Assert.Throws<AgainException>(() => { message = listener.ReceiveString(out peerName); });
            }
        }

        [Test]        
        public void SubscribeToDifferentTopic()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);

                listener.Configure(9999);
                listener.Subscribe("B");

                // this should send one broadcast message and stop
                speaker.Publish("Hello");

                ISocketPollable socket = listener;
                socket.Socket.Options.ReceiveTimeout = TimeSpan.FromSeconds(2);

                Assert.Throws<AgainException>(() =>
                {
                    string peerName;
                    listener.ReceiveString(out peerName);
                });
            }
        }

        [Test]
        public void Polling()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);
                Console.WriteLine(speaker.Hostname);

                speaker.Publish("Hello");

                var manualResetEvent = new ManualResetEvent(false);

                listener.Configure(9999);
                listener.Subscribe("H");

                string peerName = "";
                string message = "";

                listener.ReceiveReady += (sender, args) =>
                {
                    message = listener.ReceiveString(out peerName);
                    manualResetEvent.Set();
                };

                using (var poller = new Poller(listener))
                {
                    poller.PollTillCancelledNonBlocking();

                    manualResetEvent.WaitOne();

                    Console.WriteLine(peerName);

                    Assert.AreEqual("Hello", message);

                    poller.CancelAndJoin();
                }
            }
        }

        [Test]
        public void NeverConfigured()
        {
            using (var context = NetMQContext.Create())
            using (new NetMQBeacon(context))
            {}
        }

        [Test]
        public void ConfigureTwice()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(5555);
                speaker.Configure(9999);
                Console.WriteLine(speaker.Hostname);

                speaker.Publish("Hello");

                listener.Configure(9999);
                listener.Subscribe("H");

                string peerName;
                string message = listener.ReceiveString(out peerName);

                Console.WriteLine(peerName);

                Assert.AreEqual("Hello", message);
            }
        }

        [Test]
        public void BothSpeakerAndListener()
        {
            using (var context = NetMQContext.Create())
            using (var beacon1 = new NetMQBeacon(context))
            using (var beacon2 = new NetMQBeacon(context))
            {
                beacon1.Configure(9999);
                beacon1.Publish("H1");
                beacon1.Subscribe("H");

                beacon2.Configure(9999);
                beacon2.Publish("H2");
                beacon2.Subscribe("H");

                string peerName;
                string message = beacon1.ReceiveString(out peerName);

                Assert.AreEqual("H2", message);

                message = beacon2.ReceiveString(out peerName);

                Assert.AreEqual("H1",message);
            }
        }
    }
}
