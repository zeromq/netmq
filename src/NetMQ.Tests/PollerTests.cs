// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Diagnostics;
using System.Threading;
#if !PRE_4
using System.Threading.Tasks;
#endif
using NetMQ.Monitoring;
using NetMQ.zmq;
using NUnit.Framework;

// The tests within this mode merit a timeout, as under failure they can go forever. jh

namespace NetMQ.Tests
{
    [TestFixture, Timeout(300000)]
    public class PollerTests
    {
        public const string HintAboutThreadLoad = "but this can be influenced by a heavily-loaded box";

        [Test, Timeout(60000)]
        public void ResponsePoll()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var rep = contex.CreateResponseSocket())
                {
                    rep.Bind("tcp://127.0.0.1:5002");

                    using (var req = contex.CreateRequestSocket())
                    {
                        req.Connect("tcp://127.0.0.1:5002");

                        Poller poller = new Poller();

                        rep.ReceiveReady += (s, a) =>
                            {
                                bool more;
                                string m = a.Socket.ReceiveString(out more);

                                Assert.False(more);
                                Assert.AreEqual("Hello", m);

                                a.Socket.Send("World");
                            };

                        poller.AddSocket(rep);

#if !PRE_4
                        Task pollerTask = Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

                        req.Send("Hello");

                        bool more2;
                        string m1 = req.ReceiveString(out more2);

                        Assert.IsFalse(more2);
                        Assert.AreEqual("World", m1);

                        poller.Stop();

                        Thread.Sleep(100);
#if !PRE_4
                        Assert.IsTrue(pollerTask.IsCompleted);
#else
                        Assert.IsFalse(pollerThread.IsAlive);
#endif
                    }
                }
            }
        }

        [Test, Timeout(60000)]
        public void Monitoring()
        {
            ManualResetEvent listeningEvent = new ManualResetEvent(false);
            ManualResetEvent acceptedEvent = new ManualResetEvent(false);
            ManualResetEvent connectedEvent = new ManualResetEvent(false);


            using (NetMQContext contex = NetMQContext.Create())
            {
                Poller poller = new Poller();

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
#if !PRE_4
                                    var pollerTask = Task.Factory.StartNew(poller.Start);
#else
                                    var pollerThread = new Thread(_ => poller.Start() );
                                    pollerThread.Start();
#endif

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

        [Test, Timeout(60000)]
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
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");

                        bool router1arrived = false;
                        bool router2arrived = false;

                        Poller poller = new Poller();

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

#if !PRE_4
                        Task task = Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

                        dealer.Send("1");
                        Thread.Sleep(300);
                        dealer2.Send("2");
                        Thread.Sleep(300);

                        poller.Stop(true);
#if !PRE_4
                        task.Wait();
#else
                        pollerThread.Join();
#endif

                        Assert.IsTrue(router1arrived);
                        Assert.IsTrue(router2arrived);
                    }
                }
            }
        }

        [Test, Timeout(60000)]
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
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");

                        bool router1arrived = false;
                        bool router2arrived = false;
                        bool router3arrived = false;


                        Poller poller = new Poller();

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

#if !PRE_4
                        Task task = Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start());
                        pollerThread.Start();
#endif

                        dealer.Send("1");
                        Thread.Sleep(300);
                        dealer2.Send("2");
                        Thread.Sleep(300);
                        dealer3.Send("3");
                        Thread.Sleep(300);

                        poller.Stop(true);
#if !PRE_4
                        task.Wait();
#else
                        pollerThread.Join();
#endif

                        Assert.IsTrue(router1arrived);
                        Assert.IsTrue(router2arrived);
                        Assert.IsTrue(router3arrived);
                    }
                }
            }
        }

        [Test, Timeout(60000)]
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
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");
                        dealer4.Connect("tcp://127.0.0.1:5005");


                        int router1arrived = 0;
                        int router2arrived = 0;
                        bool router3arrived = false;
                        bool router4arrived = false;

                        Poller poller = new Poller();

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

#if !PRE_4
                        Task task = Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

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
#if !PRE_4
                        task.Wait();
#else
                        pollerThread.Join();
#endif

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


        [Test, Timeout(60000)]
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
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");
                        dealer2.Connect("tcp://127.0.0.1:5003");
                        dealer3.Connect("tcp://127.0.0.1:5004");

                        Poller poller = new Poller();

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

#if !PRE_4
                        Task pollerTask = Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

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
#if !PRE_4
                        Assert.IsTrue(pollerTask.IsCompleted);
#else
                        Assert.IsFalse(pollerThread.IsAlive);
#endif
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
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");

                        Poller poller = new Poller();

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

#if !PRE_4
                        Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

                        Thread.Sleep(150);

                        dealer.Send("hello");

                        Thread.Sleep(300);

                        poller.Stop();

                        Assert.IsTrue(messageArrived, "a message should have arrived.");
                        Assert.IsTrue(timerTriggered, "timer should have been triggered, " + HintAboutThreadLoad);
                        Assert.AreEqual(1, count, "count should be 1");
                    }
                }
            }
        }

        [Test, Timeout(60000)]
        public void CancelTimer()
        {
            // Use Router and Dealer sockets; add a timer to a Poller set to 100ms.
            // Set the Router socket so that upon the ReceiveReady event it calls Receive twice
            // and removes the timer.
            // Start the poller on a new thread.
            // Sleep 20ms, send a message on the Dealer socket, sleep 300ms, and stop the poller.
            // Verify that a message has been received on the Router socket,
            // and that, since the timer was removed before it had triggered - that it never triggered.
            using (NetMQContext contex = NetMQContext.Create())
            {
                // we are using three responses to make sure we actually move the correct socket and other sockets still work
                using (var router = contex.CreateRouterSocket())
                {
                    router.Bind("tcp://127.0.0.1:5002");

                    using (var dealer = contex.CreateDealerSocket())
                    {
                        dealer.Connect("tcp://127.0.0.1:5002");

                        Poller poller = new Poller();

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

#if !PRE_4
                        Task.Factory.StartNew(poller.Start);
#else
                        var pollerThread = new Thread(_ => poller.Start() );
                        pollerThread.Start();
#endif

                        Thread.Sleep(20);

                        dealer.Send("hello");

                        Thread.Sleep(300);

                        poller.Stop();

                        Assert.IsTrue(messageArrived, "message should have arrived.");
                        Assert.IsFalse(timerTriggered, "timer should NOT have been triggered, " + HintAboutThreadLoad);
                    }
                }
            }
        }

        [Test, Timeout(60000)]
        public void RunMultipleTimes()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                Poller poller = new Poller();

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

#if !PRE_4
                Task.Factory.StartNew(poller.Start);
#else
                var pollerThread = new Thread(_ => poller.Start() );
                pollerThread.Start();
#endif

                Thread.Sleep(300);

                poller.Stop();

                Assert.AreEqual(3, count);
            }
        }

        [Test, Timeout(60000)]
        public void TwoTimers()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                Poller poller = new Poller();

                int count = 0;

                NetMQTimer timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));

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

#if !PRE_4
                Task.Factory.StartNew(poller.Start);
#else
                var pollerThread = new Thread(_ => poller.Start() );
                pollerThread.Start();
#endif

                Thread.Sleep(300);

                poller.Stop();

                // While timer is stopped after 3 ticks, 50ms per tick = 150 ms,
                // if that were exact then timer2 would have time for 6 ticks, x 24ms = 144ms.
                // But these are not actually that exact, thus we are asserting that count2 is
                // at least 5, although it could be 6.  (james hurst)
                Assert.AreEqual(3, count);
                Assert.GreaterOrEqual(count2, 5);
                Assert.LessOrEqual(count2, 6);
            }
        }

        [Test, Timeout(60000)]
        public void EnableTimer()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                Poller poller = new Poller();

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

#if !PRE_4
                Task.Factory.StartNew(poller.Start);
#else
                var pollerThread = new Thread(_ => poller.Start() );
                pollerThread.Start();
#endif

                Thread.Sleep(300);

                poller.Stop();

                Assert.AreEqual(2, count);
                Assert.AreEqual(1, count2);
            }
        }

        [Test, Timeout(60000)]
        public void ChangeTimerInterval()
        {
            using (NetMQContext contex = NetMQContext.Create())
            {
                Poller poller = new Poller();

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
#if !PRE_4
                        stopwatch.Restart();
#else
                        stopwatch.Reset();
                        stopwatch.Start();
#endif
                    }
                    else if (count == 3)
                    {
                        length2 = stopwatch.ElapsedMilliseconds;

                        stopwatch.Stop();

                        timer.Enable = false;
                    }
                };

                poller.AddTimer(timer);

#if !PRE_4
                Task.Factory.StartNew(poller.Start);
#else
                var pollerThread = new Thread(_ => poller.Start() );
                pollerThread.Start();
#endif

                Thread.Sleep(500);

                poller.Stop();

                Assert.AreEqual(3, count);

                Console.WriteLine("Length1:{0}, Length2:{1}", length1, length2);

                Assert.GreaterOrEqual(length1, 8);
                Assert.LessOrEqual(length1, 12, HintAboutThreadLoad);

                Assert.GreaterOrEqual(length2, 18);
                Assert.LessOrEqual(length2, 22, HintAboutThreadLoad);
            }
        }


    }
}
