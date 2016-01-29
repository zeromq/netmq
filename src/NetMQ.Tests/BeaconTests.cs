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
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
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
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
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
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
            {
                speaker.Configure(9999);

                listener.Configure(9999);
                listener.Subscribe("H");

                // this should send one broadcast message and stop
                speaker.Publish("Hello", s_publishInterval);
                Thread.Sleep(10);
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
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
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
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
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

                using (var poller = new NetMQPoller { listener })
                {
                    poller.RunAsync();

                    manualResetEvent.WaitOne();

                    Console.WriteLine(peerName);

                    Assert.AreEqual("Hello", message);
                }
            }
        }

        [Test]
        public void NeverConfigured()
        {
            using (new NetMQBeacon())
            {}
        }

        [Test]
        public void ConfigureTwice()
        {
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
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
            using (var beacon1 = new NetMQBeacon())
            using (var beacon2 = new NetMQBeacon())
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

        [Test]
        public void BothSpeakerAndListenerOverLoopback()
        {
            using (var beacon1 = new NetMQBeacon())
            using (var beacon2 = new NetMQBeacon())
            {
                beacon1.Configure("loopback", 9998);
                beacon1.Publish("H1", s_publishInterval);
                beacon1.Subscribe("H");

                beacon2.Configure("loopback", 9998);
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
