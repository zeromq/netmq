using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using Xunit;

// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    // Note: For these tests,
    //       On Windows, you need to install PGM socket support - which comes with MSMQ:
    //       https://msdn.microsoft.com/en-us/library/aa967729%28v=vs.110%29.aspx
    //
    // Note: The 224.0.0.1 is the IPv4 All Hosts multicast group which addresses all hosts on the same network segment.

    [Trait("Category", "PGM")]
    public class PgmTests : IClassFixture<CleanupAfterFixture>
    {
        public PgmTests() => NetMQConfig.Cleanup();

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void SimplePubSub()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void BindBothSockets()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Bind("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void ConnectBothSockets()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Connect("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void UseInterface()
        {
#if NETCOREAPP1_0
            var hostEntry = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
#else
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
#endif

            string ip = hostEntry.AddressList
                .Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
                .Select(addr => addr.ToString())
                .FirstOrDefault();

            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Connect($"pgm://{ip};224.0.0.1:5555");
                sub.Bind($"pgm://{ip};224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void SetPgmSettings()
        {
            const int MegaBit = 1024;
            const int MegaByte = 1024;

            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Options.MulticastHops = 2;
                pub.Options.MulticastRate = 40*MegaBit; // 40 megabit
                pub.Options.MulticastRecoveryInterval = TimeSpan.FromMinutes(10);
                pub.Options.SendBuffer = MegaByte*10; // 10 megabyte

                pub.Connect("pgm://224.0.0.1:5555");

                sub.Options.ReceiveBuffer = MegaByte*10;
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);

                Assert.Equal(2, pub.Options.MulticastHops);
                Assert.Equal(40*MegaBit, pub.Options.MulticastRate);
                Assert.Equal(TimeSpan.FromMinutes(10), pub.Options.MulticastRecoveryInterval);
                Assert.Equal(MegaByte*10, pub.Options.SendBuffer);
                Assert.Equal(MegaByte*10, sub.Options.ReceiveBuffer);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void TwoSubscribers()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            using (var sub2 = new SubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");
                sub2.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");
                sub2.Subscribe("");

                pub.SendFrame("Hi");

                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);

                Assert.Equal("Hi", sub2.ReceiveFrameString(out more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void TwoPublishers()
        {
            using (var pub = new PublisherSocket())
            using (var pub2 = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                pub2.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.SendFrame("Hi");


                Assert.Equal("Hi", sub.ReceiveFrameString(out bool more));
                Assert.False(more);

                pub2.SendFrame("Hi2");

                Assert.Equal("Hi2", sub.ReceiveFrameString(out more));
                Assert.False(more);
            }
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void Sending1000Messages()
        {
            // creating two different context and sending 1000 messages

            int count = 0;

            var subReady = new ManualResetEvent(false);

            Task subTask = Task.Factory.StartNew(() =>
            {
                using (var sub = new SubscriberSocket())
                {
                    sub.Bind("pgm://224.0.0.1:5555");
                    sub.Subscribe("");

                    subReady.Set();

                    while (count < 1000)
                    {
                        Assert.Equal(count, BitConverter.ToInt32(sub.ReceiveFrameBytes(out bool more), 0));
                        Assert.False(more);
                        count++;
                    }
                }
            });

            subReady.WaitOne();

            Task pubTask = Task.Factory.StartNew(() =>
            {
                using (var pub = new PublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    for (int i = 0; i < 1000; i++)
                        pub.SendFrame(BitConverter.GetBytes(i));

                    // if we close the socket before the subscriber receives all messages subscriber
                    // might miss messages, lets wait another second
                    Thread.Sleep(1000);
                }
            });

            pubTask.Wait();
            subTask.Wait();

            Assert.Equal(1000, count);
        }

        [Fact(Skip = "Requires MSMQ for PGM sockets")]
        public void LargeMessage()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                var data = new byte[3200]; // this should be at least 3 packets

                for (Int16 i = 0; i < 1600; i++)
                    Array.Copy(BitConverter.GetBytes(i), 0, data, i*2, 2);

                pub.SendFrame(data);

                byte[] message = sub.ReceiveFrameBytes();

                Assert.Equal(3200, message.Length);

                for (Int16 i = 0; i < 1600; i++)
                    Assert.Equal(i, BitConverter.ToInt16(message, i*2));
            }
        }

        [Theory(Skip = "Requires MSMQ for PGM sockets")]
        [InlineData("pgm://239.0.0.1:1000")]
        [InlineData("tcp://localhost:60000")]
        public void SubscriberCleanupOnUnbind(string address)
        {
            for (var i = 0; i < 10; i++)
            {
                using (var sub = new SubscriberSocket())
                {
                    sub.Bind(address);
                    using (var monitor = new NetMQMonitor(sub, String.Format("inproc://cleanup.test{0}", Guid.NewGuid()), SocketEvents.Closed))
                    {
                        var monitorTask = Task.Factory.StartNew(monitor.Start);

                        var closed = new ManualResetEventSlim();

                        monitor.Closed += (sender, args) => closed.Set();

                        var time = DateTime.Now;

                        sub.Unbind(address);

                        // Unbind failed to report Closed event to the Monitor
                        Assert.True(closed.Wait(1000));
//                        var duration = DateTime.Now - time;

                        monitor.Stop();

                        monitorTask.Wait();
                    }
                }
            }
        }
    }
}
