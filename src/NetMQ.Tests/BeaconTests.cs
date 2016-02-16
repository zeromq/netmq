using System;
using System.Threading;
using NUnit.Framework;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    [TestFixture(Category = "Beacon")]
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

                speaker.Publish("Hello", s_publishInterval);

                listener.Configure(9999);
                listener.Subscribe("H");

                var message = listener.Receive();

                Console.WriteLine(message.PeerAddress);

                Assert.AreEqual("Hello", message.String);
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

                Assert.AreEqual("Hello", listener.Receive().String);

                BeaconMessage message;
                Assert.IsFalse(listener.TryReceive(TimeSpan.FromMilliseconds(300), out message));
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

                Assert.AreEqual("Hello", listener.Receive().String);

                BeaconMessage message;
                Assert.IsFalse(listener.TryReceive(TimeSpan.FromMilliseconds(300), out message));
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

                BeaconMessage message;
                Assert.IsFalse(listener.TryReceive(TimeSpan.FromMilliseconds(300), out message));
            }
        }

        [Test]
        public void Polling()
        {
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
            {
                speaker.Configure(9999);

                speaker.Publish("Hello", s_publishInterval);

                var manualResetEvent = new ManualResetEvent(false);

                listener.Configure(9999);
                listener.Subscribe("H");

                string message = "";

                listener.ReceiveReady += (sender, args) =>
                {
                    message = listener.Receive().String;
                    manualResetEvent.Set();
                };

                using (var poller = new NetMQPoller { listener })
                {
                    poller.RunAsync();

                    manualResetEvent.WaitOne();

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

                speaker.Publish("Hello", s_publishInterval);

                listener.Configure(9999);
                listener.Subscribe("H");

                string message = listener.Receive().String;

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

                Assert.AreEqual("H2", beacon1.Receive().String);
                Assert.AreEqual("H1", beacon2.Receive().String);
            }
        }

        [Test]
        public void BothSpeakerAndListenerOverLoopback()
        {
            using (var beacon1 = new NetMQBeacon())
            using (var beacon2 = new NetMQBeacon())
            {
                beacon1.Configure(9998, "loopback");
                beacon1.Publish("H1", s_publishInterval);
                beacon1.Subscribe("H");

                beacon2.Configure(9998, "loopback");
                beacon2.Publish("H2", s_publishInterval);
                beacon2.Subscribe("H");

                Assert.AreEqual("H2", beacon1.Receive().String);
                Assert.AreEqual("H1", beacon2.Receive().String);
            }
        }
    }
}
