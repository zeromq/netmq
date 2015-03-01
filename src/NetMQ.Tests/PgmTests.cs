using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
    // Note: For these tests,
    //       On Windows, you need to install PGM socket support - which comes with MSMQ:
    //       https://msdn.microsoft.com/en-us/library/aa967729%28v=vs.110%29.aspx
    //
    // Note: The 224.0.0.1 is the IPv4 All Hosts multicast group which addresses all hosts on the same network segment.

    [TestFixture(Category = "PGM")]
    public class PgmTests
    {
        [Test]
        public void SimplePubSub()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }

        [Test]
        public void BindBothSockets()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Bind("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }

        [Test]
        public void ConnectBothSockets()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Connect("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }

        [Test]
        public void UseInterface()
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());

            string ip = hostEntry.AddressList
                .Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
                .Select(addr => addr.ToString())
                .FirstOrDefault();

            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Connect(string.Format("pgm://{0};224.0.0.1:5555", ip));
                sub.Bind(string.Format("pgm://{0};224.0.0.1:5555", ip));

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }

        [Test]
        public void SetPgmSettings()
        {
            const int MegaBit = 1024;
            const int MegaByte = 1024;

            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Options.MulticastHops = 2;
                pub.Options.MulticastRate = 40*MegaBit; // 40 megabit
                pub.Options.MulticastRecoveryInterval = TimeSpan.FromMinutes(10);
                pub.Options.SendBuffer = MegaByte*10; // 10 megabyte

                pub.Connect("pgm://224.0.0.1:5555");

                sub.Options.ReceiveBuffer = MegaByte*10;
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);

                Assert.AreEqual(2, pub.Options.MulticastHops);
                Assert.AreEqual(40*MegaBit, pub.Options.MulticastRate);
                Assert.AreEqual(TimeSpan.FromMinutes(10), pub.Options.MulticastRecoveryInterval);
                Assert.AreEqual(MegaByte*10, pub.Options.SendBuffer);
                Assert.AreEqual(MegaByte*10, sub.Options.ReceiveBuffer);
            }
        }

        [Test]
        public void TwoSubscribers()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            using (var sub2 = context.CreateSubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");
                sub2.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");
                sub2.Subscribe("");

                pub.Send("Hi");

                bool more;
                Assert.AreEqual("Hi", sub.ReceiveString(out more));
                Assert.IsFalse(more);

                Assert.AreEqual("Hi", sub2.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }

        [Test]
        public void TwoPublishers()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var pub2 = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                pub2.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                pub.Send("Hi");

                bool more;
                string message = sub.ReceiveString(out more);

                Assert.IsFalse(more);
                Assert.AreEqual("Hi", message);

                pub2.Send("Hi2");

                message = sub.ReceiveString(out more);

                Assert.IsFalse(more);
                Assert.AreEqual("Hi2", message);
            }
        }

        [Test]
        public void Sending1000Messages()
        {
            // creating two different context and sending 1000 messages

            int count = 0;

            var subReady = new ManualResetEvent(false);

            Task subTask = Task.Factory.StartNew(() =>
            {
                using (var context = NetMQContext.Create())
                using (var sub = context.CreateSubscriberSocket())
                {
                    sub.Bind("pgm://224.0.0.1:5555");
                    sub.Subscribe("");

                    subReady.Set();

                    while (count < 1000)
                    {
                        bool more;
                        byte[] data = sub.Receive(out more);

                        Assert.IsFalse(more);
                        int num = BitConverter.ToInt32(data, 0);

                        Assert.AreEqual(num, count);

                        count++;
                    }
                }
            });

            subReady.WaitOne();

            Task pubTask = Task.Factory.StartNew(() =>
            {
                using (NetMQContext context = NetMQContext.Create())
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    for (int i = 0; i < 1000; i++)
                        pub.Send(BitConverter.GetBytes(i));

                    // if we close the socket before the subscriber receives all messages subscriber
                    // might miss messages, lets wait another second
                    Thread.Sleep(1000);
                }
            });

            pubTask.Wait();
            subTask.Wait();

            Thread.MemoryBarrier();

            Assert.AreEqual(1000, count);
        }

        [Test]
        public void LargeMessage()
        {
            using (NetMQContext context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Connect("pgm://224.0.0.1:5555");
                sub.Bind("pgm://224.0.0.1:5555");

                sub.Subscribe("");

                var data = new byte[3200]; // this should be at least 3 packets

                for (Int16 i = 0; i < 1600; i++)
                    Array.Copy(BitConverter.GetBytes(i), 0, data, i*2, 2);

                pub.Send(data);

                byte[] message = sub.Receive();

                Assert.AreEqual(3200, message.Length);

                for (Int16 i = 0; i < 1600; i++)
                    Assert.AreEqual(i, BitConverter.ToInt16(message, i*2));
            }
        }
    }
}
