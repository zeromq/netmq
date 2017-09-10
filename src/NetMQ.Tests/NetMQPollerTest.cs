using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using Xunit;

#if !NET35
using System.Collections.Concurrent;
#endif

// ReSharper disable AccessToDisposedClosure

namespace NetMQ.Tests
{
    [Trait("Category", "Poller")]
    public class NetMQPollerTest : IClassFixture<CleanupAfterFixture>
    {
        public NetMQPollerTest() => NetMQConfig.Cleanup();

        #region Socket polling tests

        [Fact]
        public void ResponsePoll()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            using (var poller = new NetMQPoller { rep })
            {
                int port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);

                rep.ReceiveReady += (s, e) =>
                {
                    Assert.Equal("Hello", e.Socket.ReceiveFrameString(out bool more));
                    Assert.False(more);

                    e.Socket.SendFrame("World");
                };

                poller.RunAsync();

                req.SendFrame("Hello");

                Assert.Equal("World", req.ReceiveFrameString(out bool more2));
                Assert.False(more2);
            }
        }

        [Fact]
        public void Monitoring()
        {
            var listeningEvent = new ManualResetEvent(false);
            var acceptedEvent = new ManualResetEvent(false);
            var connectedEvent = new ManualResetEvent(false);

            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            using (var poller = new NetMQPoller())
            using (var repMonitor = new NetMQMonitor(rep, "inproc://rep.inproc", SocketEvents.Accepted | SocketEvents.Listening))
            using (var reqMonitor = new NetMQMonitor(req, "inproc://req.inproc", SocketEvents.Connected))
            {
                repMonitor.Accepted += (s, e) => acceptedEvent.Set();
                repMonitor.Listening += (s, e) => listeningEvent.Set();

                repMonitor.AttachToPoller(poller);

                int port = rep.BindRandomPort("tcp://127.0.0.1");

                reqMonitor.Connected += (s, e) => connectedEvent.Set();

                reqMonitor.AttachToPoller(poller);

                poller.RunAsync();

                req.Connect("tcp://127.0.0.1:" + port);
                req.SendFrame("a");

                rep.SkipFrame();

                rep.SendFrame("b");

                req.SkipFrame();

                Assert.True(listeningEvent.WaitOne(300));
                Assert.True(connectedEvent.WaitOne(300));
                Assert.True(acceptedEvent.WaitOne(300));
            }
        }

        [Fact]
        public void AddSocketDuringWork()
        {
            using (var router1 = new RouterSocket())
            using (var router2 = new RouterSocket())
            using (var dealer1 = new DealerSocket())
            using (var dealer2 = new DealerSocket())
            using (var poller = new NetMQPoller { router1 })
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);

                bool router1Arrived = false;
                bool router2Arrived = false;

                var signal1 = new ManualResetEvent(false);
                var signal2 = new ManualResetEvent(false);

                router1.ReceiveReady += (s, e) =>
                {
                    router1.SkipFrame();
                    router1.SkipFrame();
                    router1Arrived = true;
                    poller.Add(router2);
                    signal1.Set();
                };

                router2.ReceiveReady += (s, e) =>
                {
                    router2.SkipFrame();
                    router2.SkipFrame();
                    router2Arrived = true;
                    signal2.Set();
                };

                poller.RunAsync();

                dealer1.SendFrame("1");
                Assert.True(signal1.WaitOne(300));

                dealer2.SendFrame("2");
                Assert.True(signal2.WaitOne(300));

                poller.Stop();

                Assert.True(router1Arrived);
                Assert.True(router2Arrived);
            }
        }

        [Fact]
        public void AddSocketAfterRemoving()
        {
            using (var router1 = new RouterSocket())
            using (var router2 = new RouterSocket())
            using (var router3 = new RouterSocket())
            using (var dealer1 = new DealerSocket())
            using (var dealer2 = new DealerSocket())
            using (var dealer3 = new DealerSocket())
            using (var poller = new NetMQPoller { router1, router2 })
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);

                bool router1Arrived = false;
                bool router2Arrived = false;
                bool router3Arrived = false;

                var signal1 = new ManualResetEvent(false);
                var signal2 = new ManualResetEvent(false);
                var signal3 = new ManualResetEvent(false);

                router1.ReceiveReady += (s, e) =>
                {
                    router1Arrived = true;
                    router1.SkipFrame();
                    router1.SkipFrame();
                    poller.Remove(router1);
                    signal1.Set();
                };

                router2.ReceiveReady += (s, e) =>
                {
                    router2Arrived = true;
                    router2.SkipFrame();
                    router2.SkipFrame();
                    poller.Add(router3);
                    signal2.Set();
                };

                router3.ReceiveReady += (s, e) =>
                {
                    router3Arrived = true;
                    router3.SkipFrame();
                    router3.SkipFrame();
                    signal3.Set();
                };

                poller.RunAsync();

                dealer1.SendFrame("1");
                Assert.True(signal1.WaitOne(300));
                dealer2.SendFrame("2");
                Assert.True(signal2.WaitOne(300));
                dealer3.SendFrame("3");
                Assert.True(signal3.WaitOne(300));

                poller.Stop();

                Assert.True(router1Arrived);
                Assert.True(router2Arrived);
                Assert.True(router3Arrived);
            }
        }

        [Fact]
        public void AddTwoSocketAfterRemoving()
        {
            using (var router1 = new RouterSocket())
            using (var router2 = new RouterSocket())
            using (var router3 = new RouterSocket())
            using (var router4 = new RouterSocket())
            using (var dealer1 = new DealerSocket())
            using (var dealer2 = new DealerSocket())
            using (var dealer3 = new DealerSocket())
            using (var dealer4 = new DealerSocket())
            using (var poller = new NetMQPoller { router1, router2 })
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");
                int port4 = router4.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);
                dealer4.Connect("tcp://127.0.0.1:" + port4);

                int router1Arrived = 0;
                int router2Arrived = 0;
                bool router3Arrived = false;
                bool router4Arrived = false;

                var signal1 = new ManualResetEvent(false);
                var signal2 = new ManualResetEvent(false);
                var signal3 = new ManualResetEvent(false);
                var signal4 = new ManualResetEvent(false);

                router1.ReceiveReady += (s, e) =>
                {
                    router1Arrived++;
                    router1.SkipFrame(); // identity
                    router1.SkipFrame(); // message
                    poller.Remove(router1);
                    signal1.Set();
                };

                router2.ReceiveReady += (s, e) =>
                {
                    router2Arrived++;
                    router2.SkipFrame(); // identity
                    router2.SkipFrame(); // message

                    if (router2Arrived == 1)
                    {
                        poller.Add(router3);
                        poller.Add(router4);
                    }

                    signal2.Set();
                };

                router3.ReceiveReady += (s, e) =>
                {
                    router3.SkipFrame(); // identity
                    router3.SkipFrame(); // message
                    router3Arrived = true;
                    signal3.Set();
                };

                router4.ReceiveReady += (s, e) =>
                {
                    router4.SkipFrame(); // identity
                    router4.SkipFrame(); // message
                    router4Arrived = true;
                    signal4.Set();
                };

                poller.RunAsync();

                dealer1.SendFrame("1");
                Assert.True(signal1.WaitOne(300));
                dealer2.SendFrame("2");
                Assert.True(signal2.WaitOne(300));
                signal2.Reset();
                dealer3.SendFrame("3");
                dealer4.SendFrame("4");
                dealer2.SendFrame("2");
                dealer1.SendFrame("1");
                Assert.True(signal3.WaitOne(300));
                Assert.True(signal4.WaitOne(300));
                Assert.True(signal2.WaitOne(300));

                poller.Stop();

                router1.SkipFrame();
                Assert.Equal("1", router1.ReceiveFrameString(out bool more));
                Assert.False(more);

                Assert.Equal(1, router1Arrived);
                Assert.Equal(2, router2Arrived);
                Assert.True(router3Arrived);
                Assert.True(router4Arrived);
            }
        }


        [Fact]
        public void RemoveSocket()
        {
            using (var router1 = new RouterSocket())
            using (var router2 = new RouterSocket())
            using (var router3 = new RouterSocket())
            using (var dealer1 = new DealerSocket())
            using (var dealer2 = new DealerSocket())
            using (var dealer3 = new DealerSocket())
            using (var poller = new NetMQPoller { router1, router2, router3 })
            {
                int port1 = router1.BindRandomPort("tcp://127.0.0.1");
                int port2 = router2.BindRandomPort("tcp://127.0.0.1");
                int port3 = router3.BindRandomPort("tcp://127.0.0.1");

                dealer1.Connect("tcp://127.0.0.1:" + port1);
                dealer2.Connect("tcp://127.0.0.1:" + port2);
                dealer3.Connect("tcp://127.0.0.1:" + port3);

                bool first = true;

                router1.ReceiveReady += (s, e) =>
                {
                    // Should only be called once
                    Assert.True(first);
                    first = false;

                    // identity
                    e.Socket.SkipFrame();

                    Assert.Equal("Hello", e.Socket.ReceiveFrameString(out bool more));
                    Assert.False(more);

                    // cancelling the socket
                    poller.Remove(e.Socket); // remove self
                };

                router2.ReceiveReady += (s, e) =>
                {
                    // identity
                    byte[] identity = e.Socket.ReceiveFrameBytes();

                    // message
                    e.Socket.SkipFrame();

                    e.Socket.SendMoreFrame(identity);
                    e.Socket.SendFrame("2");
                };

                router3.ReceiveReady += (s, e) =>
                {
                    // identity
                    byte[] identity = e.Socket.ReceiveFrameBytes();

                    // message
                    e.Socket.SkipFrame();

                    e.Socket.SendMoreFrame(identity).SendFrame("3");
                };

                Task pollerTask = Task.Factory.StartNew(poller.Run);

                // Send three messages. Only the first will be processed, as then handler removes
                // the socket from the poller.
                dealer1.SendFrame("Hello");
                dealer1.SendFrame("Hello2");
                dealer1.SendFrame("Hello3");

                // making sure the socket defined before the one cancelled still works
                dealer2.SendFrame("1");
                Assert.Equal("2", dealer2.ReceiveFrameString());

                // making sure the socket defined after the one cancelled still works
                dealer3.SendFrame("1");
                Assert.Equal("3", dealer3.ReceiveFrameString());

                poller.Stop();
                // await the pollerTask, 1ms should suffice
                pollerTask.Wait(1);
                Assert.True(pollerTask.IsCompleted);
            }
        }

        [Fact]
        public void AddThrowsIfSocketAlreadyDisposed()
        {
            var poller = new NetMQPoller();

            var socket = new RouterSocket();

            // Dispose the socket.
            // It is incorrect to have a disposed socket in a poller.
            // Disposed sockets can throw into the poller's thread.
            socket.Dispose();

            // Adding a disposed socket throws
            var ex = Assert.Throws<ArgumentException>(() => poller.Add(socket));

            Assert.True(ex.Message.StartsWith("Must not be disposed."));
            Assert.Equal("socket", ex.ParamName);

            // Still dispose it. It throws after cleanup.
            Assert.Throws<NetMQException>(() => poller.Dispose());
        }

        [Fact]
        public void RemoveThrowsIfSocketAlreadyDisposed()
        {
            var socket = new RouterSocket();

            var poller = new NetMQPoller { socket };

            // Dispose the socket.
            // It is incorrect to have a disposed socket in a poller.
            // Disposed sockets can throw into the poller's thread.
            socket.Dispose();

            // Remove throws if the removed socket
            var ex = Assert.Throws<ArgumentException>(() => poller.Remove(socket));

            Assert.True(ex.Message.StartsWith("Must not be disposed."));
            Assert.Equal("socket", ex.ParamName);

            // Still dispose it. It throws after cleanup.
            Assert.Throws<NetMQException>(() => poller.Dispose());
        }

        [Fact]
        public void DisposeThrowsIfSocketAlreadyDisposed()
        {
            var socket = new RouterSocket();

            var poller = new NetMQPoller { socket };

            // Dispose the socket.
            // It is incorrect to have a disposed socket in a poller.
            // Disposed sockets can throw into the poller's thread.
            socket.Dispose();

            // Dispose throws if a polled socket is disposed
            var ex = Assert.Throws<NetMQException>(() => poller.Dispose());

            Assert.Equal("Invalid state detected: NetMQPoller contains a disposed NetMQSocket. Sockets must be either removed before being disposed, or disposed after the poller is disposed.", ex.Message);
        }

        [Fact]
        public void SimpleTimer()
        {
            // TODO it is not really clear what this test is actually testing -- maybe split it into a few smaller tests

            using (var router = new RouterSocket())
            using (var dealer = new DealerSocket())
            using (var poller = new NetMQPoller { router })
            {
                int port = router.BindRandomPort("tcp://127.0.0.1");

                dealer.Connect("tcp://127.0.0.1:" + port);

                bool messageArrived = false;

                router.ReceiveReady += (s, e) =>
                {
                    Assert.False(messageArrived);
                    router.SkipFrame();
                    router.SkipFrame();
                    messageArrived = true;
                };

                bool timerTriggered = false;

                int count = 0;

                const int timerIntervalMillis = 100;

                var timer = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis));
                timer.Elapsed += (s, a) =>
                {
                    // the timer should jump before the message
                    Assert.False(messageArrived);
                    timerTriggered = true;
                    timer.Enable = false;
                    count++;
                };
                poller.Add(timer);

                poller.RunAsync();

                Thread.Sleep(150);

                dealer.SendFrame("hello");

                Thread.Sleep(300);

                poller.Stop();

                Assert.True(messageArrived);
                Assert.True(timerTriggered);
                Assert.Equal(1, count);
            }
        }

        [Fact]
        public void RemoveTimer()
        {
            using (var router = new RouterSocket())
            using (var dealer = new DealerSocket())
            using (var poller = new NetMQPoller { router })
            {
                int port = router.BindRandomPort("tcp://127.0.0.1");

                dealer.Connect("tcp://127.0.0.1:" + port);

                bool timerTriggered = false;

                var timer = new NetMQTimer(TimeSpan.FromMilliseconds(100));
                timer.Elapsed += (s, a) => { timerTriggered = true; };

                // The timer will fire after 100ms
                poller.Add(timer);

                bool messageArrived = false;

                router.ReceiveReady += (s, e) =>
                {
                    router.SkipFrame();
                    router.SkipFrame();
                    messageArrived = true;
                    // Remove timer
                    poller.Remove(timer);
                };

                poller.RunAsync();

                Thread.Sleep(20);

                dealer.SendFrame("hello");

                Thread.Sleep(300);

                poller.Stop();

                Assert.True(messageArrived);
                Assert.False(timerTriggered);
            }
        }

        [Fact]
        public void RunMultipleTimes()
        {
            int count = 0;

            const int timerIntervalMillis = 20;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis));
            timer.Elapsed += (s, a) =>
            {
                count++;

                if (count == 3)
                {
                    timer.Enable = false;
                }
            };

            using (var poller = new NetMQPoller { timer })
            {
                poller.RunAsync();

                Thread.Sleep(timerIntervalMillis * 6);

                poller.Stop();

                Assert.Equal(3, count);
            }
        }

/*
        NOTE PollOnce hasn't been ported from Poller to NetMQPoller. Is it needed?

        [Fact]
        public void PollOnce()
        {
            int count = 0;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(50));
            timer.Elapsed += (s, a) =>
            {
                count++;

                if (count == 3)
                {
                    timer.Enable = false;
                }
            };

            // NOTE if the PollTimeout here is less than the timer period, it won't fire during PollOnce -- is this by design?

            using (var poller = new NetMQPoller { timer })
            {
                poller.PollTimeout = TimeSpan.FromSeconds(1);

                var stopwatch = Stopwatch.StartNew();

                poller.PollOnce();

                var pollOnceElapsedTime = stopwatch.ElapsedMilliseconds;

                Assert.Equal(1, count, "the timer should have fired just once during the call to PollOnce()");
                Assert.Less(pollOnceElapsedTime, 90, "pollonce should return soon after the first timer firing.");
            }
        }
*/

        [Fact]
        public void TwoTimers()
        {
            var timer1 = new NetMQTimer(TimeSpan.FromMilliseconds(60));
            var timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(40));

            int count = 0;
            int count2 = 0;

            var signal1 = new ManualResetEvent(false);
            var signal2 = new ManualResetEvent(false);

            timer1.Elapsed += (s, a) =>
            {
                count++;
                timer1.Enable = false;
                timer2.Enable = false;
                signal1.Set();
            };

            timer2.Elapsed += (s, e) =>
            {
                count2++;
                signal2.Set();
            };

            using (var poller = new NetMQPoller { timer1, timer2 })
            {
                poller.RunAsync();

                Assert.True(signal1.WaitOne(300));
                Assert.True(signal2.WaitOne(300));

                poller.Stop();
            }

            Assert.Equal(1, count);
            Assert.Equal(1, count2);
        }

        [Fact]
        public void EnableTimer()
        {
            const int timerIntervalMillis = 20;

            var timer1 = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis));
            var timer2 = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis)) { Enable = false};

            int count = 0;
            int count2 = 0;

            timer1.Elapsed += (s, a) =>
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

            timer2.Elapsed += (s, e) =>
            {
                timer1.Enable = true;
                timer2.Enable = false;

                count2++;
            };

            using (var poller = new NetMQPoller { timer1, timer2 })
            {
                poller.RunAsync();

                Thread.Sleep(timerIntervalMillis * 6);

                poller.Stop();
            }

            Assert.Equal(2, count);
            Assert.Equal(1, count2);
        }

        [Fact]
        public void ChangeTimerInterval()
        {
            int count = 0;

            const int timerIntervalMillis = 10;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis));

            var stopwatch = new Stopwatch();

            long length1 = 0;
            long length2 = 0;

            timer.Elapsed += (s, a) =>
            {
                count++;

                if (count == 1)
                {
                    stopwatch.Start();
                    timer.Interval = 30;
                }
                else if (count == 2)
                {
                    length1 = stopwatch.ElapsedMilliseconds;

                    timer.Interval = 60;
                    stopwatch.Restart();
                }
                else if (count == 3)
                {
                    length2 = stopwatch.ElapsedMilliseconds;

                    stopwatch.Stop();

                    timer.Enable = false;
                }
            };

            using (var poller = new NetMQPoller { timer })
            {
                poller.RunAsync();

                Thread.Sleep(200);

                poller.Stop();
            }

            Assert.Equal(3, count);

            Assert.True(Math.Abs(length1 - 30) <= 10.0);
            Assert.True(Math.Abs(length2 - 60) <= 10.0);
        }

        [Fact]
        public void TestPollerDispose()
        {
            const int timerIntervalMillis = 10;

            var timer = new NetMQTimer(TimeSpan.FromMilliseconds(timerIntervalMillis));

            var signal = new ManualResetEvent(false);

            var count = 0;

            timer.Elapsed += (s, a) =>
            {
                if (count++ == 5)
                    signal.Set();
            };

            NetMQPoller poller;
            using (poller = new NetMQPoller { timer })
            {
                poller.RunAsync();
                Assert.True(signal.WaitOne(500));
                Assert.True(poller.IsRunning);
                Assert.Throws<InvalidOperationException>(() => poller.Run());
            }

            Assert.False(poller.IsRunning);
            Assert.Throws<ObjectDisposedException>(() => poller.Run());
            Assert.Throws<ObjectDisposedException>(() => poller.Stop());
            Assert.Throws<ObjectDisposedException>(() => poller.Add(timer));
            Assert.Throws<ObjectDisposedException>(() => poller.Remove(timer));
        }

        [Fact]
        public void NativeSocket()
        {
            using (var streamServer = new StreamSocket())
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                int port = streamServer.BindRandomPort("tcp://*");

                socket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), port));

                var buffer = new byte[] { 1 };
                socket.Send(buffer);

                byte[] identity = streamServer.ReceiveFrameBytes();
                byte[] message = streamServer.ReceiveFrameBytes();

                Assert.Equal(buffer[0], message[0]);

                var socketSignal = new ManualResetEvent(false);

                using (var poller = new NetMQPoller())
                {
                    poller.Add(socket, s =>
                    {
                        socket.Receive(buffer);

                        socketSignal.Set();

                        // removing the socket
                        poller.Remove(socket);
                    });

                    poller.RunAsync();

                    // no message is waiting for the socket so it should fail
                    Assert.False(socketSignal.WaitOne(100));

                    // sending a message back to the socket
                    streamServer.SendMoreFrame(identity).SendFrame("a");

                    Assert.True(socketSignal.WaitOne(100));

                    socketSignal.Reset();

                    // sending a message back to the socket
                    streamServer.SendMoreFrame(identity).SendFrame("a");

                    // we remove the native socket so it should fail
                    Assert.False(socketSignal.WaitOne(100));

                    poller.Stop();
                }
            }
        }

        #endregion

        #region TaskScheduler tests

#if !NET35
        [Fact]
        public void OneTask()
        {
            bool triggered = false;

            using (var poller = new NetMQPoller())
            {
                poller.RunAsync();

                var task = new Task(() =>
                {
                    triggered = true;
                    Assert.True(poller.CanExecuteTaskInline, "Should be on NetMQPoller thread");
                });
                task.Start(poller);
                task.Wait();

                Assert.True(triggered);
            }
        }

        [Fact]
        public void SetsCurrentTaskScheduler()
        {
            using (var poller = new NetMQPoller())
            {
                poller.RunAsync();

                var task = new Task(() => Assert.Same(TaskScheduler.Current, poller));
                task.Start(poller);
                task.Wait();
            }
        }

        [Fact]
        public void CanExecuteTaskInline()
        {
            using (var poller = new NetMQPoller())
            {
                Assert.False(poller.CanExecuteTaskInline);

                poller.RunAsync();

                Assert.False(poller.CanExecuteTaskInline);

                var task = new Task(() => Assert.True(poller.CanExecuteTaskInline));
                task.Start(poller);
                task.Wait();
            }
        }

        [Fact]
        public void ContinueWith()
        {
            int threadId1 = 0;
            int threadId2 = 1;

            int runCount1 = 0;
            int runCount2 = 0;

            using (var poller = new NetMQPoller())
            {
                poller.RunAsync();

                var task = new Task(() =>
                {
                    threadId1 = Thread.CurrentThread.ManagedThreadId;
                    runCount1++;
                });

                var task2 = task.ContinueWith(t =>
                {
                    threadId2 = Thread.CurrentThread.ManagedThreadId;
                    runCount2++;
                }, poller);

                task.Start(poller);
                task.Wait();
                task2.Wait();

                Assert.Equal(threadId1, threadId2);
                Assert.Equal(1, runCount1);
                Assert.Equal(1, runCount2);
            }
        }

        [Fact]
        public void TwoThreads()
        {
            int count1 = 0;
            int count2 = 0;

            var allTasks = new ConcurrentBag<Task>();

            using (var poller = new NetMQPoller())
            {
                poller.RunAsync();

                Task t1 = Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var task = new Task(() => { count1++; });
                        allTasks.Add(task);
                        task.Start(poller);
                    }
                });

                Task t2 = Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var task = new Task(() => { count2++; });
                        allTasks.Add(task);
                        task.Start(poller);
                    }
                });

                t1.Wait(1000);
                t2.Wait(1000);
                Task.WaitAll(allTasks.ToArray(), 1000);

                Assert.Equal(100, count1);
                Assert.Equal(100, count2);
            }
        }
#endif

        #endregion

        #region ISynchronizeInvoke tests

#if NET451
        [Fact]
        public void ISynchronizeInvokeWorks()
        {
            using (var poller = new NetMQPoller())
            using (var signal = new ManualResetEvent(false))
            using (var timer = new System.Timers.Timer { AutoReset = false, Interval = 100, SynchronizingObject = poller, Enabled = true })
            {
                var isCorrectThread = false;

                poller.RunAsync();

                timer.Elapsed += (sender, args) =>
                {
                    isCorrectThread = poller.CanExecuteTaskInline;
                    Assert.True(signal.Set());
                };

                Assert.True(signal.WaitOne(TimeSpan.FromSeconds(2)));
                Assert.True(isCorrectThread);
            }
        }
#endif

#endregion
    }
}
