using System;
using System.Threading;
using NUnit.Framework;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    [TestFixture]
    public class BeaconTests
    {
        private static readonly TimeSpan s_publishInterval = TimeSpan.FromMilliseconds(100);

        [Test]
        public void SimplePublishSubscribe()
        {
            using (var context = NetMQContext.Create())
            using (var speaker = new NetMQBeacon(context))
            using (var listener = new NetMQBeacon(context))
            {
                speaker.Configure(9999);
                Console.WriteLine(speaker.Hostname);

                speaker.Publish("Hello", s_publishInterval);

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
                speaker.Publish("Hello", s_publishInterval);
                Thread.Sleep(10);
                speaker.Silence();

                string peerName;
                Assert.AreEqual("Hello", listener.ReceiveString(out peerName));

                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromMilliseconds(300), out peerName, out message));
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
                speaker.Publish("Hello", s_publishInterval);

                listener.Unsubscribe();

                string peerName;
                Assert.AreEqual("Hello", listener.ReceiveString(out peerName));

                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromMilliseconds(300), out peerName, out message));
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
                speaker.Publish("Hello", s_publishInterval);

                string peerName;
                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromMilliseconds(300), out peerName, out message));
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

                speaker.Publish("Hello", s_publishInterval);

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

                using (var poller = new Poller(listener) { PollTimeout = 10})
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

                speaker.Publish("Hello", s_publishInterval);

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
                beacon1.Publish("H1", s_publishInterval);
                beacon1.Subscribe("H");

                beacon2.Configure(9999);
                beacon2.Publish("H2", s_publishInterval);
                beacon2.Subscribe("H");

                string peerName;
                string message = beacon1.ReceiveString(out peerName);

                Assert.AreEqual("H2", message);

                message = beacon2.ReceiveString(out peerName);

                Assert.AreEqual("H1", message);
            }
        }
    }
}
