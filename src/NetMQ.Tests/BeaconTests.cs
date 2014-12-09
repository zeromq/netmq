using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class BeaconTests
    {
        [Test]
        public void SimplePublishSubscribe()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(9999);
                    Console.WriteLine(speaker.Hostname);

                    speaker.Publish("Hello");

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
                        listener.Configure(9999);
                        listener.Subscribe("H");

                        string peerName;
                        string message = listener.ReceiveString(out peerName);

                        Console.WriteLine(peerName);

                        Assert.AreEqual("Hello", message);                        
                    }
                }
            }
        }

        [Test]
        public void Silence()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(9999);                                        

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
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

                        Assert.Throws<AgainException>(() =>
                        {
                            message = listener.ReceiveString(out peerName);
                        });
                    }
                }
            }
        }

        [Test]
        public void Unsubscribe()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(9999);

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
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

                        Assert.Throws<AgainException>(() =>
                        {
                            message = listener.ReceiveString(out peerName);
                        });
                    }
                }
            }
        }

        [Test]        
        public void SubscribeToDifferentTopic()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(9999);

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
                        listener.Configure(9999);
                        listener.Subscribe("B");

                        // this should send one broadcast message and stop
                        speaker.Publish("Hello");                        
            

                        ISocketPollable socket = listener;
                        socket.Socket.Options.ReceiveTimeout = TimeSpan.FromSeconds(2);

                        Assert.Throws<AgainException>(() =>
                        {
                            string peerName;
                            string message = listener.ReceiveString(out peerName);
                        });
                    }
                }
            }
        }

        [Test]
        public void Polling()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(9999);
                    Console.WriteLine(speaker.Hostname);

                    speaker.Publish("Hello");

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
                        ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                        listener.Configure(9999);
                        listener.Subscribe("H");

                        string peerName = "";
                        string message= "";

                        listener.ReceiveReady += (sender, args) =>
                        {                            
                            message = listener.ReceiveString(out peerName);
                            manualResetEvent.Set();
                        };

                        Poller poller = new Poller(listener);
                        poller.PollTillCancelledNonBlocking();                        
                        
                        manualResetEvent.WaitOne();
                        
                        Console.WriteLine(peerName);

                        Assert.AreEqual("Hello", message);

                        poller.Stop(true);
                    }
                }
            }
        }

        [Test]
        public void NeverConfigured()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                }
            }
        }

        [Test]
        public void ConfigureTwice()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQBeacon speaker = new NetMQBeacon(context))
                {
                    speaker.Configure(5555);
                    speaker.Configure(9999);
                    Console.WriteLine(speaker.Hostname);

                    speaker.Publish("Hello");

                    using (NetMQBeacon listener = new NetMQBeacon(context))
                    {
                        listener.Configure(9999);
                        listener.Subscribe("H");

                        string peerName;
                        string message = listener.ReceiveString(out peerName);

                        Console.WriteLine(peerName);

                        Assert.AreEqual("Hello", message);
                    }
                }
            }
        }       
    }
}
