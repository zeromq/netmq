// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
#if !PRE_4
using System.Threading.Tasks;
#endif
using NUnit.Framework;


namespace NetMQ.Tests
{
    [TestFixture(Category = "PGM")]
    public class PgmTests
    {
        [Test]
        public void SimplePubSub()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Bind("pgm://224.0.0.1:5555");

                        sub.Subscribe("");

                        pub.Send("Hi");

                        bool more;
                        string message = sub.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Hi", message);
                    }
                }
            }
        }

        [Test]
        public void BindBothSockets()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Bind("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Bind("pgm://224.0.0.1:5555");

                        sub.Subscribe("");

                        pub.Send("Hi");

                        bool more;
                        string message = sub.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Hi", message);
                    }
                }
            }
        }

        [Test]
        public void ConnectBothSockets()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Connect("pgm://224.0.0.1:5555");

                        sub.Subscribe("");

                        pub.Send("Hi");

                        bool more;
                        string message = sub.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Hi", message);
                    }
                }
            }
        }

        [Test]
        public void UseInterface()
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            string ip = (
                                 from addr in hostEntry.AddressList
                                 where addr.AddressFamily == AddressFamily.InterNetwork
                                 select addr.ToString()
                    ).FirstOrDefault();

            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect(string.Format("pgm://{0};224.0.0.1:5555", ip));

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Bind(string.Format("pgm://{0};224.0.0.1:5555", ip));

                        sub.Subscribe("");

                        pub.Send("Hi");

                        bool more;
                        string message = sub.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Hi", message);
                    }
                }
            }
        }

        [Test]
        public void SetPgmSettings()
        {
            const int MegaBit = 1024;
            const int MegaByte = 1024;

            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Options.MulticastHops = 2;
                    pub.Options.MulticastRate = 40 * MegaBit; // 40 megabit
                    pub.Options.MulticastRecoveryInterval = TimeSpan.FromMinutes(10);
                    pub.Options.SendBuffer = MegaByte * 10; // 10 megabyte

                    pub.Connect("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Options.ReceivevBuffer = MegaByte * 10;
                        sub.Bind("pgm://224.0.0.1:5555");

                        sub.Subscribe("");

                        pub.Send("Hi");

                        bool more;
                        string message = sub.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Hi", message);

                        Assert.AreEqual(2, pub.Options.MulticastHops);
                        Assert.AreEqual(40 * MegaBit, pub.Options.MulticastRate);
                        Assert.AreEqual(TimeSpan.FromMinutes(10), pub.Options.MulticastRecoveryInterval);
                        Assert.AreEqual(MegaByte * 10, pub.Options.SendBuffer);
                        Assert.AreEqual(MegaByte * 10, sub.Options.ReceivevBuffer);
                    }
                }
            }
        }

        [Test]
        public void TwoSubscribers()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        using (var sub2 = context.CreateSubscriberSocket())
                        {
                            sub.Bind("pgm://224.0.0.1:5555");
                            sub2.Bind("pgm://224.0.0.1:5555");

                            sub.Subscribe("");
                            sub2.Subscribe("");

                            pub.Send("Hi");

                            bool more;
                            string message = sub.ReceiveString(out more);

                            Assert.IsFalse(more);
                            Assert.AreEqual("Hi", message);

                            message = sub2.ReceiveString(out more);

                            Assert.IsFalse(more);
                            Assert.AreEqual("Hi", message);
                        }
                    }
                }
            }
        }

        [Test]
        public void TwoPublishers()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");
                    using (var pub2 = context.CreatePublisherSocket())
                    {
                        pub2.Connect("pgm://224.0.0.1:5555");

                        using (var sub = context.CreateSubscriberSocket())
                        {
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
                }
            }
        }


        [Test]
        public void Sending1000Messages()
        {
            // creating two different context and sending 1000 messages

            int count = 0;

            ManualResetEvent subReady = new ManualResetEvent(false);

#if !PRE_4
            Task subTask = Task.Factory.StartNew(() =>
#else
            var subThread = new Thread(_ =>
#endif
                 {
                     using (NetMQContext context = NetMQContext.Create())
                     {
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
                     }
                 });

            subReady.WaitOne();

#if !PRE_4
            Task pubTask = Task.Factory.StartNew(() =>
#else
            var pubThread = new Thread(_ =>
#endif
                 {
                     using (NetMQContext context = NetMQContext.Create())
                     {
                         using (var pub = context.CreatePublisherSocket())
                         {
                             pub.Connect("pgm://224.0.0.1:5555");

                             for (int i = 0; i < 1000; i++)
                             {
                                 pub.Send(BitConverter.GetBytes(i));
                             }
                         }
                     }
                 });

            // Wait for both threads to finish..
#if !PRE_4
            pubTask.Wait();
            subTask.Wait();
#else
            pubThread.Join();
            subThread.Join();
#endif

            Thread.MemoryBarrier();

            Assert.AreEqual(1000, count);
        }


        [Test]
        public void LargeMessage()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pub = context.CreatePublisherSocket())
                {
                    pub.Connect("pgm://224.0.0.1:5555");

                    using (var sub = context.CreateSubscriberSocket())
                    {
                        sub.Bind("pgm://224.0.0.1:5555");

                        sub.Subscribe("");

                        byte[] data = new byte[3200]; // this should be at least 3 packets

                        for (Int16 i = 0; i < 1600; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(i), 0, data, i * 2, 2);
                        }

                        pub.Send(data);
                        bool more;
                        byte[] message = sub.Receive(out more);

                        Assert.AreEqual(3200, message.Length);

                        for (Int16 i = 0; i < 1600; i++)
                        {
                            Int16 value = BitConverter.ToInt16(message, i * 2);

                            Assert.AreEqual(i, value);
                        }
                    }
                }
            }
        }
    }
}

