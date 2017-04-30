using System;
using System.Threading;
using Xunit;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    [Trait("Category", "Beacon")]
    public class BeaconTests : IClassFixture<CleanupAfterFixture>
    {
        private static readonly TimeSpan s_publishInterval = TimeSpan.FromMilliseconds(100);

        public BeaconTests() => NetMQConfig.Cleanup();

        [Fact]
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

                Assert.Equal("Hello", message.String);
            }
        }

        [Fact]
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

                Assert.Equal("Hello", listener.Receive().String);

                Assert.False(listener.TryReceive(TimeSpan.FromMilliseconds(300), out BeaconMessage message));
            }
        }

        [Fact]
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

                Assert.Equal("Hello", listener.Receive().String);

                Assert.False(listener.TryReceive(TimeSpan.FromMilliseconds(300), out BeaconMessage message));
            }
        }

        [Fact]
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

                Assert.False(listener.TryReceive(TimeSpan.FromMilliseconds(300), out BeaconMessage message));
            }
        }

        [Fact]
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

                    Assert.Equal("Hello", message);
                }
            }
        }

        [Fact]
        public void NeverConfigured()
        {
            using (new NetMQBeacon())
            {}
        }

        [Fact]
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

                Assert.Equal("Hello", message);
            }
        }

        [Fact]
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

                Assert.Equal("H2", beacon1.Receive().String);
                Assert.Equal("H1", beacon2.Receive().String);
            }
        }

        [Fact]
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

                Assert.Equal("H2", beacon1.Receive().String);
                Assert.Equal("H1", beacon2.Receive().String);
            }
        }
    }
}
