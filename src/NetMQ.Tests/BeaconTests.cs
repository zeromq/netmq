using System;
using System.Threading;
using NUnit.Framework;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    [TestFixture]
    public class BeaconTests
    {
        // TODO allow beacon publish period to be specified, in order to make these tests faster (or use a WaitHandle to allow signalling)

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
                Assert.AreEqual("Hello", listener.ReceiveString(out peerName));

                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromSeconds(2), out peerName, out message));
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

                listener.Unsubscribe();

                string peerName;
                Assert.AreEqual("Hello", listener.ReceiveString(out peerName));

                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromSeconds(2), out peerName, out message));
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

                string peerName;
                string message;
                Assert.IsFalse(listener.TryReceiveString(TimeSpan.FromSeconds(2), out peerName, out message));
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

                Assert.AreEqual("H1", message);
            }
        }
    }
}
