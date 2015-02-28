using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
    // Note: you can have failures here if you excute these on a machine that has only one processor-core.

    [TestFixture]
    public class PollerTests
    {
        [Test]
        public void ResponsePoll()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            using (var poller = new Poller(rep))
            {
                int port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);

                rep.ReceiveReady += (s, a) =>
                {
                    bool more;
                    Assert.AreEqual("Hello", a.Socket.ReceiveString(out more));
                    Assert.False(more);

                    a.Socket.Send("World");
                };

                Task pollerTask = Task.Factory.StartNew(poller.PollTillCancelled);

                req.Send("Hello");

                bool more2;
                Assert.AreEqual("World", req.ReceiveString(out more2));
                Assert.IsFalse(more2);

                poller.CancelAndJoin();

                Thread.Sleep(100);
                Assert.IsTrue(pollerTask.IsCompleted);
            }
        }

        [Test]
        public void Monitoring()
        {
            var listeningEvent = new ManualResetEvent(false);
            var acceptedEvent = new ManualResetEvent(false);
            var connectedEvent = new ManualResetEvent(false);

            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            using (var poller = new Poller())
            using (var repMonitor = new NetMQMonitor(context, rep, "inproc://rep.inproc", SocketEvent.Accepted | SocketEvent.Listening))
            using (var reqMonitor = new NetMQMonitor(context, req, "inproc://req.inproc", SocketEvent.Connected))
            {
                repMonitor.Accepted += (s, a) => acceptedEvent.Set();
                repMonitor.Listening += (s, a) => listeningEvent.Set();

                repMonitor.AttachToPoller(poller);

                int port = rep.BindRandomPort("tcp://127.0.0.1");

                reqMonitor.Connected += (s, a) => connectedEvent.Set();

                reqMonitor.AttachToPoller(poller);
                try
                {
                    poller.PollTillCancelledNonBlocking();

                    req.Connect("tcp://127.0.0.1:" + port);
                    req.Send("a");

                    rep.ReceiveString();

                    rep.Send("b");

                    req.ReceiveString();

                    Assert.IsTrue(listeningEvent.WaitOne(300));
                    Assert.IsTrue(connectedEvent.WaitOne(300));
                    Assert.IsTrue(acceptedEvent.WaitOne(300));
                }
                finally
                {
                    poller.CancelAndJoin();
                }
            }
        }

        [Test]
        public void AddSocketDuringWork()
        {
            using (var context = NetMQContext.Create())
            using (var router1 = context.CreateRouterSocket())
            using (var router2 = context.CreateRouterSocket())
            using (var dealer1 = context.CreateDealerSocket())
            using (var dealer2 = context.CreateDealerSocket())
            using (var poller = new Poller(router1))
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);

                bool router1arrived = false;
                bool router2arrived = false;

                router1.ReceiveReady += (s, a) =>
                {
                    router1arrived = true;

                    router1.Receive();
                    router1.Receive();

                    poller.AddSocket(router2);
                };

                router2.ReceiveReady += (s, a) =>
                {
                    router2.Receive();
                    router2.Receive();
                    router2arrived = true;
                };

                poller.PollTillCancelledNonBlocking();

                dealer1.Send("1");
                Thread.Sleep(300);
                dealer2.Send("2");
                Thread.Sleep(300);

                poller.CancelAndJoin();

                Assert.IsTrue(router1arrived);
                Assert.IsTrue(router2arrived);
            }
        }

        [Test]
        public void AddSocketAfterRemoving()
        {
            using (var context = NetMQContext.Create())
            using (var router1 = context.CreateRouterSocket())
            using (var router2 = context.CreateRouterSocket())
            using (var router3 = context.CreateRouterSocket())
            using (var dealer1 = context.CreateDealerSocket())
            using (var dealer2 = context.CreateDealerSocket())
            using (var dealer3 = context.CreateDealerSocket())
            using (var poller = new Poller(router1, router2))
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);

                bool router1arrived = false;
                bool router2arrived = false;
                bool router3arrived = false;

                router1.ReceiveReady += (s, a) =>
                {
                    router1arrived = true;

                    router1.Receive();
                    router1.Receive();
                    poller.RemoveSocket(router1);
                };

                router2.ReceiveReady += (s, a) =>
                {
                    router2arrived = true;
                    router2.Receive();
                    router2.Receive();
                    poller.AddSocket(router3);
                };

                router3.ReceiveReady += (s, a) =>
                {
                    router3arrived = true;
                    router3.Receive();
                    router3.Receive();
                };

                poller.PollTillCancelledNonBlocking();

                dealer1.Send("1");
                Thread.Sleep(300);
                dealer2.Send("2");
                Thread.Sleep(300);
                dealer3.Send("3");
                Thread.Sleep(300);

                poller.CancelAndJoin();

                Assert.IsTrue(router1arrived);
                Assert.IsTrue(router2arrived);
                Assert.IsTrue(router3arrived);
            }
        }

        [Test]
        public void AddTwoSocketAfterRemoving()
        {
            using (var context = NetMQContext.Create())
            using (var router1 = context.CreateRouterSocket())
            using (var router2 = context.CreateRouterSocket())
            using (var router3 = context.CreateRouterSocket())
            using (var router4 = context.CreateRouterSocket())
            using (var dealer1 = context.CreateDealerSocket())
            using (var dealer2 = context.CreateDealerSocket())
            using (var dealer3 = context.CreateDealerSocket())
            using (var dealer4 = context.CreateDealerSocket())
            using (var poller = new Poller(router1, router2))
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");
                int port4 = router4.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);
                dealer4.Connect("tcp://127.0.0.1:" + port4);

                int router1arrived = 0;
                int router2arrived = 0;
                bool router3arrived = false;
                bool router4arrived = false;

                router1.ReceiveReady += (s, a) =>
                {
                    router1arrived++;

                    router1.Receive();
                    router1.Receive();

                    poller.RemoveSocket(router1);
                };

                router2.ReceiveReady += (s, a) =>
                {
                    router2arrived++;
                    router2.Receive();
                    router2.Receive();

                    if (router2arrived == 1)
                    {
                        poller.AddSocket(router3);

                        poller.AddSocket(router4);
                    }
                };

                router3.ReceiveReady += (s, a) =>
                {
                    router3.Receive();
                    router3.Receive();
                    router3arrived = true;
                };

                router4.ReceiveReady += (s, a) =>
                {
                    router4.Receive();
                    router4.Receive();
                    router4arrived = true;
                };

                poller.PollTillCancelledNonBlocking();

                dealer1.Send("1");
                Thread.Sleep(300);
                dealer2.Send("2");
                Thread.Sleep(300);
                dealer3.Send("3");
                dealer4.Send("4");
                dealer2.Send("2");
                dealer1.Send("1");
                Thread.Sleep(300);

                poller.CancelAndJoin();

                bool more;
                
                router1.Receive(true, out more);
                Assert.IsTrue(more);

                router1.Receive(true, out more);
                Assert.IsFalse(more);

                Assert.AreEqual(1, router1arrived);
                Assert.AreEqual(2, router2arrived);
                Assert.IsTrue(router3arrived);
                Assert.IsTrue(router4arrived);
            }
        }


        [Test]
        public void CancelSocket()
        {
            using (var context = NetMQContext.Create())
            using (var router1 = context.CreateRouterSocket())
            using (var router2 = context.CreateRouterSocket())
            using (var router3 = context.CreateRouterSocket())
            using (var dealer1 = context.CreateDealerSocket())
            using (var dealer2 = context.CreateDealerSocket())
            using (var dealer3 = context.CreateDealerSocket())
            using (var poller = new Poller(router1, router2, router3))
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);

                bool first = true;

                router1.ReceiveReady += (s, a) =>
                {
                    if (!first)
                    {
                        Assert.Fail("This should happen because we cancelled the socket");
                    }
                    first = false;

                    // identity
                    a.Socket.Receive();

                    bool more;
                    Assert.AreEqual("Hello", a.Socket.ReceiveString(out more));
                    Assert.False(more);

                    // cancelling the socket
                    poller.RemoveSocket(a.Socket);
                };

                router2.ReceiveReady += (s, a) =>
                {
                    // identity
                    byte[] identity = a.Socket.Receive();

                    // message
                    a.Socket.Receive();

                    a.Socket.SendMore(identity);
                    a.Socket.Send("2");
                };

                router3.ReceiveReady += (s, a) =>
                {
                    // identity
                    byte[] identity = a.Socket.Receive();

                    // message
                    a.Socket.Receive();

                    a.Socket.SendMore(identity).Send("3");
                };

                Task pollerTask = Task.Factory.StartNew(poller.PollTillCancelled);

                dealer1.Send("Hello");

                // sending this should not arrive on the poller, therefore response for this will never arrive
                dealer1.Send("Hello2");

                Thread.Sleep(100);

                // sending this should not arrive on the poller, therefore response for this will never arrive						
                dealer1.Send("Hello3");

                Thread.Sleep(500);

                // making sure the socket defined before the one cancelled still works
                dealer2.Send("1");
                Assert.AreEqual("2", dealer2.ReceiveString());

                // making sure the socket defined after the one cancelled still works
                dealer3.Send("1");
                Assert.AreEqual("3", dealer3.ReceiveString());

                // we have to give this some time if we want to make sure it's really not happening and it not only because of time
                Thread.Sleep(300);

                poller.CancelAndJoin();

                Thread.Sleep(100);
                Assert.IsTrue(pollerTask.IsCompleted);
            }
        }

        [Test]
        public void SimpleTimer()
        {
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            using (var dealer = context.CreateDealerSocket())
            using (var poller = new Poller(router))
            {
                int port = router.BindRandomPort("tcp://127.0.0.1");

                dealer.Connect("tcp://127.0.0.1:" + port);

                bool messageArrived = false;

                router.ReceiveReady += (s, a) =>
                {
                    router.Receive();
                    router.Receive();
                    messageArrived = true;
                };

                bool timerTriggered = false;

                int count = 0;

                var timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
                timer.Elapsed += (a, s) =>
                {
                    // the timer should jump before the message
                    Assert.IsFalse(messageArrived);
                    timerTriggered = true;
                    timer.Enable = false;
                    count++;
                };
                poller.AddTimer(timer);

                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(150);

                dealer.Send("hello");

                Thread.Sleep(300);

                poller.CancelAndJoin();

                Assert.IsTrue(messageArrived);
                Assert.IsTrue(timerTriggered);
                Assert.AreEqual(1, count);
            }
        }

        [Test]
        public void CancelTimer()
        {
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            using (var dealer = context.CreateDealerSocket())
            using (var poller = new Poller(router))
            {
                int port = router.BindRandomPort("tcp://127.0.0.1");

                dealer.Connect("tcp://127.0.0.1:" + port);

                bool timerTriggered = false;

                var timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
                timer.Elapsed += (a, s) => { timerTriggered = true; };

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

                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(20);

                dealer.Send("hello");

                Thread.Sleep(300);

                poller.CancelAndJoin();

                Assert.IsTrue(messageArrived);
                Assert.IsFalse(timerTriggered);
            }
        }

        [Test]
        public void RunMultipleTimes()
        {
            int count = 0;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));
            timer.Elapsed += (a, s) =>
            {
                count++;

                if (count == 3)
                {
                    timer.Enable = false;
                }
            };

            using (var poller = new Poller(timer))
            {
                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(300);

                poller.CancelAndJoin();

                Assert.AreEqual(3, count);
            }
        }

        [Test]
        public void PollOnce()
        {
            int count = 0;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));
            timer.Elapsed += (a, s) =>
            {
                count++;

                if (count == 3)
                {
                    timer.Enable = false;
                }
            };

            using (var poller = new Poller(timer))
            {
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
            var timer1 = new NetMQTimer(TimeSpan.FromMilliseconds(52));
            var timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(40));

            int count = 0;

            timer1.Elapsed += (a, s) =>
            {
                count++;
                timer1.Enable = false;
                timer2.Enable = false;
            };

            int count2 = 0;

            timer2.Elapsed += (s, a) => { count2++; };

            using (var poller = new Poller(timer1, timer2))
            {
                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(300);

                poller.CancelAndJoin();
            }

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void EnableTimer()
        {
            var timer1 = new NetMQTimer(TimeSpan.FromMilliseconds(20));
            var timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(20)) { Enable = false};

            int count = 0;
            int count2 = 0;

            timer1.Elapsed += (a, s) =>
            {
                count++;

                if (count == 1)
                {
                    timer2.Enable = true;
                    timer1.Enable = false;
                }
                else if (count == 2)
                {
                    timer1.Enable = false;
                }
            };

            timer2.Elapsed += (s, a) =>
            {
                timer1.Enable = true;
                timer2.Enable = false;

                count2++;
            };

            using (var poller = new Poller(timer1, timer2))
            {
                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(300);

                poller.CancelAndJoin();
            }

            Assert.AreEqual(2, count);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void ChangeTimerInterval()
        {
            int count = 0;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(10));

            var stopwatch = new Stopwatch();

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

            using (var poller = new Poller(timer))
            {
                poller.PollTillCancelledNonBlocking();

                Thread.Sleep(500);

                poller.CancelAndJoin();
            }

            Assert.AreEqual(3, count);

            Assert.GreaterOrEqual(length1, 8);
            Assert.LessOrEqual(length1, 12);

            Assert.GreaterOrEqual(length2, 18);
            Assert.LessOrEqual(length2, 22);
        }

        [Test]
        public void TestPollerDispose()
        {
            int count = 0;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(10));

            var stopwatch = new Stopwatch();

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
                poller.PollTillCancelledNonBlocking();
                Thread.Sleep(500);
                Assert.Throws<InvalidOperationException>(() => poller.PollTillCancelled());
            }

            Assert.That(poller.IsStarted, Is.False);
            Assert.Throws<ObjectDisposedException>(() => poller.PollTillCancelled());
            Assert.Throws<ObjectDisposedException>(() => poller.CancelAndJoin());
            Assert.Throws<ObjectDisposedException>(() => poller.AddTimer(timer));
            Assert.Throws<ObjectDisposedException>(() => poller.RemoveTimer(timer));

            Assert.AreEqual(3, count);

            Assert.GreaterOrEqual(length1, 8);
            Assert.LessOrEqual(length1, 12);

            Assert.GreaterOrEqual(length2, 18);
            Assert.LessOrEqual(length2, 22);
        }

        [Test]
        public void NativeSocket()
        {
            using (var context = NetMQContext.Create())
            using (var streamServer = context.CreateStreamSocket())
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                int port = streamServer.BindRandomPort("tcp://*");

                socket.Connect("127.0.0.1", port);

                var buffer = new byte[] { 1 };
                socket.Send(buffer);

                byte[] identity = streamServer.Receive();
                byte[] message = streamServer.Receive();

                Assert.AreEqual(buffer[0], message[0]);

                var socketSignal = new ManualResetEvent(false);

                var poller = new Poller();
                poller.AddPollInSocket(socket, s =>
                {
                    socket.Receive(buffer);

                    socketSignal.Set();

                    // removing the socket
                    poller.RemovePollInSocket(socket);
                });

                poller.PollTillCancelledNonBlocking();

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

                poller.CancelAndJoin();
            }
        }
    }
}
