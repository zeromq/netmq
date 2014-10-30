using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ.Tests
{
    [TestFixture]
    public class PollerTests
    {
        [Test]
        public void ResponsePoll()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var rep = contex.CreateResponseSocket())
                {
                    rep.Bind("tcp://127.0.0.1:5002");

                    using (var req = contex.CreateRequestSocket())
                    using (Poller poller = new Poller())
                    {
                        req.Connect("tcp://127.0.0.1:5002");


                        rep.ReceiveReady += (s, a) =>
                                                                    {
                                                                        bool more;
                                                                        string m = a.Socket.ReceiveString(out more);

                                                                        Assert.False(more);
                                                                        Assert.AreEqual("Hello", m);

                                                                        a.Socket.Send("World");
                                                                    };

                        poller.AddSocket(rep);

                        Task pollerTask = Task.Factory.StartNew(poller.Start);

                        req.Send("Hello");

                        bool more2;
                        string m1 = req.ReceiveString(out more2);

                        Assert.IsFalse(more2);
                        Assert.AreEqual("World", m1);

                        poller.Stop();

                        Thread.Sleep(100);
                        Assert.IsTrue(pollerTask.IsCompleted);
                    }
                }
            }
        }

        [Test]
        public void Monitoring()
        {
            ManualResetEvent listeningEvent = new ManualResetEvent(false);
            ManualResetEvent acceptedEvent = new ManualResetEvent(false);
            ManualResetEvent connectedEvent = new ManualResetEvent(false);


            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {
                using (var rep = contex.CreateResponseSocket())
                {
                    using (NetMQMonitor monitor = new NetMQMonitor(contex, rep, "inproc://rep.inproc", SocketEvent.Accepted | SocketEvent.Listening))
                    {
                        monitor.Accepted += (s, a) => acceptedEvent.Set();
                        monitor.Listening += (s, a) => listeningEvent.Set();

                        monitor.AttachToPoller(poller);

                        rep.Bind("tcp://127.0.0.1:5002");

                        using (var req = contex.CreateRequestSocket())
                        {
                            using (NetMQMonitor reqMonitor = new NetMQMonitor(contex, req, "inproc://req.inproc", SocketEvent.Connected))
                            {
                                reqMonitor.Connected += (s, a) => connectedEvent.Set();

                                reqMonitor.AttachToPoller(poller);
                                try
                                {
                                    var pollerTask = Task.Factory.StartNew(poller.Start);

                                    req.Connect("tcp://127.0.0.1:5002");
                                    req.Send("a");

                                    bool more;

                                    string m = rep.ReceiveString(out more);

                                    rep.Send("b");

                                    string m2 = req.ReceiveString(out more);

                                    Assert.IsTrue(listeningEvent.WaitOne(300));
                                    Assert.IsTrue(connectedEvent.WaitOne(300));
                                    Assert.IsTrue(acceptedEvent.WaitOne(300));
                                }
                                finally
                                {
                                    poller.Stop();
                                }
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void AddSocketDuringWork()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                using (var router2 = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");
                    router2.Bind("tcp://127.0.0.1:5003");

                    using (var dealer = contex.CreateDealerSocket())
                    using (var dealer2 = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");

                        bool router1arrived = false;
                        bool router2arrived = false;


                        bool more;

                        router2.ReceiveReady += (s, a) =>
                        {
                            router2.Receive(out more);
                            router2.Receive(out more);
                            router2arrived = true;
                        };

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            router1arrived = true;

                                                                            router.Receive(out more);
                                                                            router.Receive(out more);

                                                                            poller.AddSocket(router2);
                                                                        };

                        poller.AddSocket(router);

                        Task task = Task.Factory.StartNew(poller.Start);

                        dealer.Send("1");
                        Thread.Sleep(300);
                        dealer2.Send("2");
                        Thread.Sleep(300);

                        poller.Stop(true);
                        task.Wait();

                        Assert.IsTrue(router1arrived);
                        Assert.IsTrue(router2arrived);
                    }
                }
            }
        }

        [Test]
        public void AddSocketAfterRemoving()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                using (var router2 = contex.CreateRouterSocket())
                using (var router3 = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");
                    router2.Bind("tcp://127.0.0.1:5003");
                    router3.Bind("tcp://127.0.0.1:5004");

                    using (var dealer = contex.CreateDealerSocket())
                    using (var dealer2 = contex.CreateDealerSocket())
                    using (var dealer3 = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");

                        bool router1arrived = false;
                        bool router2arrived = false;
                        bool router3arrived = false;


                        bool more;

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            router1arrived = true;

                                                                            router.Receive(out more);
                                                                            router.Receive(out more);

                                                                            poller.RemoveSocket(router);

                                                                        };

                        poller.AddSocket(router);

                        router3.ReceiveReady += (s, a) =>
                        {
                            router3.Receive(out more);
                            router3.Receive(out more);
                            router3arrived = true;
                        };

                        router2.ReceiveReady += (s, a) =>
                                                                            {
                                                                                router2arrived = true;
                                                                                router2.Receive(out more);
                                                                                router2.Receive(out more);

                                                                                poller.AddSocket(router3);
                                                                            };
                        poller.AddSocket(router2);

                        Task task = Task.Factory.StartNew(poller.Start);

                        dealer.Send("1");
                        Thread.Sleep(300);
                        dealer2.Send("2");
                        Thread.Sleep(300);
                        dealer3.Send("3");
                        Thread.Sleep(300);

                        poller.Stop(true);
                        task.Wait();

                        Assert.IsTrue(router1arrived);
                        Assert.IsTrue(router2arrived);
                        Assert.IsTrue(router3arrived);
                    }
                }
            }
        }

        [Test]
        public void AddTwoSocketAfterRemoving()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                using (var router2 = contex.CreateRouterSocket())
                using (var router3 = contex.CreateRouterSocket())
                using (var router4 = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");
                    router2.Bind("tcp://127.0.0.1:5003");
                    router3.Bind("tcp://127.0.0.1:5004");
                    router4.Bind("tcp://127.0.0.1:5005");

                    using (var dealer = contex.CreateDealerSocket())
                    using (var dealer2 = contex.CreateDealerSocket())
                    using (var dealer3 = contex.CreateDealerSocket())
                    using (var dealer4 = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");
                        dealer4.Connect("tcp://127.0.0.1:5005");


                        int router1arrived = 0;
                        int router2arrived = 0;
                        bool router3arrived = false;
                        bool router4arrived = false;

                        bool more;

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            router1arrived++;

                                                                            router.Receive(out more);
                                                                            router.Receive(out more);

                                                                            poller.RemoveSocket(router);

                                                                        };

                        poller.AddSocket(router);

                        router3.ReceiveReady += (s, a) =>
                                                                            {
                                                                                router3.Receive(out more);
                                                                                router3.Receive(out more);
                                                                                router3arrived = true;
                                                                            };

                        router4.ReceiveReady += (s, a) =>
                                                                            {
                                                                                router4.Receive(out more);
                                                                                router4.Receive(out more);
                                                                                router4arrived = true;
                                                                            };

                        router2.ReceiveReady += (s, a) =>
                                                                            {
                                                                                router2arrived++;
                                                                                router2.Receive(out more);
                                                                                router2.Receive(out more);

                                                                                if (router2arrived == 1)
                                                                                {
                                                                                    poller.AddSocket(router3);

                                                                                    poller.AddSocket(router4);
                                                                                }
                                                                            };

                        poller.AddSocket(router2);

                        Task task = Task.Factory.StartNew(poller.Start);

                        dealer.Send("1");
                        Thread.Sleep(300);
                        dealer2.Send("2");
                        Thread.Sleep(300);
                        dealer3.Send("3");
                        dealer4.Send("4");
                        dealer2.Send("2");
                        dealer.Send("1");
                        Thread.Sleep(300);

                        poller.Stop(true);
                        task.Wait();

                        router.Receive(true, out more);

                        Assert.IsTrue(more);

                        router.Receive(true, out more);

                        Assert.IsFalse(more);

                        Assert.AreEqual(1, router1arrived);
                        Assert.AreEqual(2, router2arrived);
                        Assert.IsTrue(router3arrived);
                        Assert.IsTrue(router4arrived);
                    }
                }
            }
        }


        [Test]
        public void CancelSocket()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                using (var router2 = contex.CreateRouterSocket())
                using (var router3 = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");
                    router2.Bind("tcp://127.0.0.1:5003");
                    router3.Bind("tcp://127.0.0.1:5004");

                    using (var dealer = contex.CreateDealerSocket())
                    using (var dealer2 = contex.CreateDealerSocket())
                    using (var dealer3 = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");

                        bool first = true;

                        router2.ReceiveReady += (s, a) =>
                                                                            {
                                                                                bool more;

                                                                                // identity
                                                                                byte[] identity = a.Socket.Receive(out more);

                                                                                // message
                                                                                a.Socket.Receive(out more);

                                                                                a.Socket.SendMore(identity);
                                                                                a.Socket.Send("2");
                                                                            };

                        poller.AddSocket(router2);

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            if (!first)
                                                                            {
                                                                                Assert.Fail("This should happen because we cancelled the socket");
                                                                            }
                                                                            first = false;

                                                                            bool more;

                                                                            // identity
                                                                            a.Socket.Receive(out more);

                                                                            string m = a.Socket.ReceiveString(out more);

                                                                            Assert.False(more);
                                                                            Assert.AreEqual("Hello", m);

                                                                            // cancellign the socket
                                                                            poller.RemoveSocket(a.Socket);
                                                                        };

                        poller.AddSocket(router);

                        router3.ReceiveReady += (s, a) =>
                                                                            {
                                                                                bool more;

                                                                                // identity
                                                                                byte[] identity = a.Socket.Receive(out more);

                                                                                // message
                                                                                a.Socket.Receive(out more);

                                                                                a.Socket.SendMore(identity).Send("3");
                                                                            };

                        poller.AddSocket(router3);

                        Task pollerTask = Task.Factory.StartNew(poller.Start);

                        dealer.Send("Hello");

                        // sending this should not arrive on the poller, therefore response for this will never arrive
                        dealer.Send("Hello2");

                        Thread.Sleep(100);

                        // sending this should not arrive on the poller, therefore response for this will never arrive						
                        dealer.Send("Hello3");

                        Thread.Sleep(500);

                        bool more2;

                        // making sure the socket defined before the one cancelled still works
                        dealer2.Send("1");
                        string msg = dealer2.ReceiveString(out more2);
                        Assert.AreEqual("2", msg);

                        // making sure the socket defined after the one cancelled still works
                        dealer3.Send("1");
                        msg = dealer3.ReceiveString(out more2);
                        Assert.AreEqual("3", msg);

                        // we have to give this some time if we want to make sure it's really not happening and it not only because of time
                        Thread.Sleep(300);

                        poller.Stop();

                        Thread.Sleep(100);
                        Assert.IsTrue(pollerTask.IsCompleted);
                    }
                }
            }
        }

        [Test]
        public void SimpleTimer()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");

                    using (var dealer = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");


                        bool messageArrived = false;

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            bool isMore;
                                                                            router.Receive(out isMore);
                                                                            router.Receive(out isMore);


                                                                            messageArrived = true;
                                                                        };

                        poller.AddSocket(router);

                        bool timerTriggered = false;

                        int count = 0;

                        NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
                        timer.Elapsed += (a, s) =>
                                                            {
                                                                // the timer should jump before the message
                                                                Assert.IsFalse(messageArrived);
                                                                timerTriggered = true;
                                                                timer.Enable = false;
                                                                count++;
                                                            };
                        poller.AddTimer(timer);

                        Task.Factory.StartNew(poller.Start);

                        Thread.Sleep(150);

                        dealer.Send("hello");

                        Thread.Sleep(300);

                        poller.Stop();

                        Assert.IsTrue(messageArrived);
                        Assert.IsTrue(timerTriggered);
                        Assert.AreEqual(1, count);
                    }
                }
            }
        }

        [Test]
        public void CancelTimer()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");

                    using (var dealer = contex.CreateDealerSocket())
                    using (Poller poller = new Poller())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");


                        bool timerTriggered = false;

                        NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
                        timer.Elapsed += (a, s) =>
                                                            {
                                                                timerTriggered = true;
                                                            };

                        // the timer will jump after 100ms
                        poller.AddTimer(timer);

                        bool messageArrived = false;

                        router.ReceiveReady += (s, a) =>
                                                                        {
                                                                            bool isMore;
                                                                            router.Receive(out isMore);
                                                                            router.Receive(out isMore);

                                                                            messageArrived = true;
                                                                            poller.RemoveTimer(timer);
                                                                        };

                        poller.AddSocket(router);

                        Task.Factory.StartNew(poller.Start);

                        Thread.Sleep(20);

                        dealer.Send("hello");

                        Thread.Sleep(300);

                        poller.Stop();

                        Assert.IsTrue(messageArrived);
                        Assert.IsFalse(timerTriggered);
                    }
                }
            }
        }

        [Test]
        public void RunMultipleTimes()
        {
            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));
                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 3)
                    {
                        timer.Enable = false;
                    }
                };

                poller.AddTimer(timer);

                Task.Factory.StartNew(poller.Start);

                Thread.Sleep(300);

                poller.Stop();

                Assert.AreEqual(3, count);
            }
        }

        [Test]
        public void PollOnce()
        {
            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));
                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 3)
                    {
                        timer.Enable = false;
                    }
                };

                poller.AddTimer(timer);

                Stopwatch stopwatch = Stopwatch.StartNew();

                poller.PollOnce();

                var pollOnceElapsedTime = stopwatch.ElapsedMilliseconds;

                Assert.AreEqual(1, count, "the timer should have fired just once during the call to PollOnce()");
                Assert.Less(pollOnceElapsedTime, 90, "pollonce should return soon after the first timer firing.");
            }
        }

        [Test]
        public void TwoTimers()
        {
            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(52));

                NetMQTimer timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(24));


                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 3)
                    {
                        timer.Enable = false;
                        timer2.Enable = false;
                    }
                };

                poller.AddTimer(timer);

                int count2 = 0;

                timer2.Elapsed += (s, a) => { count2++; };
                poller.AddTimer(timer2);

                Task.Factory.StartNew(poller.Start);

                Thread.Sleep(300);

                poller.Stop();

                Assert.AreEqual(3, count);
                Assert.AreEqual(6, count2);
            }
        }

        [Test]
        public void EnableTimer()
        {
            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(20));

                NetMQTimer timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(20));

                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 1)
                    {
                        timer2.Enable = true;
                        timer.Enable = false;
                    }
                    else if (count == 2)
                    {
                        timer.Enable = false;
                    }
                };

                int count2 = 0;

                timer2.Elapsed += (s, a) =>
                                                        {
                                                            timer.Enable = true;
                                                            timer2.Enable = false;

                                                            count2++;
                                                        };

                timer2.Enable = false;

                poller.AddTimer(timer);
                poller.AddTimer(timer2);

                Task.Factory.StartNew(poller.Start);

                Thread.Sleep(300);

                poller.Stop();

                Assert.AreEqual(2, count);
                Assert.AreEqual(1, count2);
            }
        }

        [Test]
        public void ChangeTimerInterval()
        {
            using (NetMQContext contex = NetMQContext.Create())
            using (Poller poller = new Poller())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(10));

                Stopwatch stopwatch = new Stopwatch();

                long length1 = 0;
                long length2 = 0;

                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 1)
                    {
                        stopwatch.Start();
                    }
                    else if (count == 2)
                    {
                        length1 = stopwatch.ElapsedMilliseconds;

                        timer.Interval = 20;
                        stopwatch.Restart();
                    }
                    else if (count == 3)
                    {
                        length2 = stopwatch.ElapsedMilliseconds;

                        stopwatch.Stop();

                        timer.Enable = false;
                    }
                };

                poller.AddTimer(timer);

                Task.Factory.StartNew(poller.Start);

                Thread.Sleep(500);

                poller.Stop();

                Assert.AreEqual(3, count);

                Console.WriteLine("Length1:{0}, Length2:{1}", length1, length2);

                Assert.GreaterOrEqual(length1, 8);
                Assert.LessOrEqual(length1, 12);

                Assert.GreaterOrEqual(length2, 18);
                Assert.LessOrEqual(length2, 22);
            }
        }

        [Test]
        public void TestPollerDispose()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(10));

                Stopwatch stopwatch = new Stopwatch();

                long length1 = 0;
                long length2 = 0;


                timer.Elapsed += (a, s) =>
                {
                    count++;

                    if (count == 1)
                    {
                        stopwatch.Start();
                    }
                    else if (count == 2)
                    {
                        length1 = stopwatch.ElapsedMilliseconds;
                        timer.Interval = 20;
                        stopwatch.Restart();
                    }
                    else if (count == 3)
                    {
                        length2 = stopwatch.ElapsedMilliseconds;
                        stopwatch.Stop();
                        timer.Enable = false;
                    }
                };

                Poller poller;
                using (poller = new Poller(timer))
                {
                    Task task = Task.Factory.StartNew(poller.Start);
                    Thread.Sleep(500);
                    Assert.Throws<InvalidOperationException>(() => { poller.Start(); });
                }

                Assert.That(!poller.IsStarted);
                Assert.Throws<ObjectDisposedException>(() => { poller.Start(); });
                Assert.Throws<ObjectDisposedException>(() => { poller.Stop(); });
                Assert.Throws<ObjectDisposedException>(() => { poller.AddTimer(timer); });
                Assert.Throws<ObjectDisposedException>(() => { poller.RemoveTimer(timer); });

                Assert.AreEqual(3, count);

                Console.WriteLine("Length1:{0}, Length2:{1}", length1, length2);

                Assert.GreaterOrEqual(length1, 8);
                Assert.LessOrEqual(length1, 12);

                Assert.GreaterOrEqual(length2, 18);
                Assert.LessOrEqual(length2, 22);
            }
        }

        [Test]
        public void NativeSocket()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var streamServer = context.CreateStreamSocket())
                {
                    streamServer.Bind("tcp://*:5557");

                    using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        socket.Connect("127.0.0.1", 5557);

                        byte[] buffer = new byte[] { 1 };
                        socket.Send(buffer);

                        byte[] identity = streamServer.Receive();
                        byte[] message = streamServer.Receive();

                        Assert.AreEqual(buffer[0], message[0]);

                        ManualResetEvent socketSignal = new ManualResetEvent(false);

                        Poller poller = new Poller();
                        poller.AddPollInSocket(socket, s =>
                        {
                            socket.Receive(buffer);

                            socketSignal.Set();

                            // removing the socket
                            poller.RemovePollInSocket(socket);
                        });

                        Task.Factory.StartNew(poller.Start);

                        // no message is waiting for the socket so it should fail
                        Assert.IsFalse(socketSignal.WaitOne(200));

                        // sending a message back to the socket
                        streamServer.SendMore(identity).Send("a");

                        Assert.IsTrue(socketSignal.WaitOne(200));

                        socketSignal.Reset();

                        // sending a message back to the socket
                        streamServer.SendMore(identity).Send("a");

                        // we remove the native socket so it should fail
                        Assert.IsFalse(socketSignal.WaitOne(200));

                        poller.Stop(true);
                    }
                }
            }



        }
    }
}
