using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using Xunit;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    public class SocketTests : IClassFixture<CleanupAfterFixture>
    {
        public SocketTests() => NetMQConfig.Cleanup();

        [Fact]
        public void CheckTryReceive()
        {
            using (var router = new RouterSocket())
            {
                router.BindRandomPort("tcp://127.0.0.1");

                var msg = new Msg();
                msg.InitEmpty();
                Assert.False(router.TryReceive(ref msg, TimeSpan.Zero));
            }
        }

        [Fact]
        public void CheckTrySendSucceeds()
        {
            using (var router = new RouterSocket())
            using (var dealer = new DealerSocket())
            {
                var port = router.BindRandomPort("tcp://127.0.0.1");
                router.Options.Linger = TimeSpan.Zero;

                dealer.Options.SendHighWatermark = 1;
                dealer.Options.Linger = TimeSpan.Zero;
                dealer.Connect("tcp://127.0.0.1:" + port);

                Thread.Sleep(100);

                Assert.True(dealer.TrySendFrame("1"));
            }
        }

        [Fact]
        public void CheckTrySendFails()
        {
            using (var dealer = new DealerSocket())
            {
                dealer.Options.SendHighWatermark = 1;
                dealer.Options.Linger = TimeSpan.Zero;
                dealer.Connect("tcp://127.0.0.1:55555");

                Thread.Sleep(100);

                var success = dealer.TrySendFrame("1");
                Assert.True(success); // because the SendHighWatermark allows it into the buffers
                success = dealer.TrySendFrame("2");
                Assert.False(success);
            }
        }

        [Fact]
        public void LargeMessage()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(100);

                var msg = new byte[300];

                pub.SendFrame(msg);

                byte[] msg2 = sub.ReceiveFrameBytes();

                Assert.Equal(300, msg2.Length);
            }
        }

        [Fact]
        public void ReceiveMessageWithTimeout()
        {
            {
                var pubSync = new AutoResetEvent(false);
                var payload = new byte[300];
                const int waitTime = 500;

                var t1 = new Task(() =>
                {
                    using (var pubSocket = new PublisherSocket())
                    {
                        pubSocket.Bind("tcp://127.0.0.1:12345");
                        pubSync.WaitOne();
                        Thread.Sleep(waitTime);
                        pubSocket.SendFrame(payload);
                        pubSync.WaitOne();
                    }
                }, TaskCreationOptions.LongRunning);

                var t2 = new Task(() =>
                {
                    using (var subSocket = new SubscriberSocket())
                    {
                        subSocket.Connect("tcp://127.0.0.1:12345");
                        subSocket.Subscribe("");
                        Thread.Sleep(100);
                        pubSync.Set();

                        NetMQMessage msg = null;
                        Assert.False(subSocket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(100), ref msg));

                        Assert.True(subSocket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(waitTime), ref msg));
                        Assert.NotNull(msg);
                        Assert.Equal(1, msg.FrameCount);
                        Assert.Equal(300, msg.First.MessageSize);
                        pubSync.Set();
                    }
                }, TaskCreationOptions.LongRunning);

                t1.Start();
                t2.Start();

                Task.WaitAll(t1, t2);
            }
        }

        [Fact]
        public void LargeMessageLittleEndian()
        {
            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                pub.Options.Endian = Endianness.Little;
                sub.Options.Endian = Endianness.Little;

                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);

                sub.Subscribe("");

                Thread.Sleep(100);

                var msg = new byte[300];

                pub.SendFrame(msg);

                byte[] msg2 = sub.ReceiveFrameBytes();

                Assert.Equal(300, msg2.Length);
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public void TestKeepalive()
        {
            // there is no way to test tcp keep alive without disconnect the cable, we just testing that is not crashing the system
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                rep.Options.TcpKeepalive = true;
                rep.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                rep.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                req.Options.TcpKeepalive = true;
                req.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                req.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                var port = rep.BindRandomPort("tcp://127.0.0.1");
                req.Connect("tcp://127.0.0.1:" + port);


                req.SendFrame("1");

                Assert.Equal("1", rep.ReceiveFrameString(out bool more));
                Assert.False(more);

                rep.SendFrame("2");

                Assert.Equal("2", req.ReceiveFrameString(out more));
                Assert.False(more);

                Assert.True(req.Options.TcpKeepalive);
                Assert.Equal(TimeSpan.FromSeconds(5), req.Options.TcpKeepaliveIdle);
                Assert.Equal(TimeSpan.FromSeconds(1), req.Options.TcpKeepaliveInterval);

                Assert.True(rep.Options.TcpKeepalive);
                Assert.Equal(TimeSpan.FromSeconds(5), rep.Options.TcpKeepaliveIdle);
                Assert.Equal(TimeSpan.FromSeconds(1), rep.Options.TcpKeepaliveInterval);
            }
        }

        [Fact]
        public void MultipleLargeMessages()
        {
            var largeMessage = new byte[12000];

            for (int i = 0; i < 12000; i++)
                largeMessage[i] = (byte)(i % 256);

            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(1000);

                pub.SendFrame("");
                sub.SkipFrame();

                for (int i = 0; i < 100; i++)
                {
                    pub.SendFrame(largeMessage);

                    byte[] recvMessage = sub.ReceiveFrameBytes();

                    Assert.Equal(largeMessage, recvMessage);
                }
            }
        }

        [Fact]
        public void LargerBufferLength()
        {
            var largerBuffer = new byte[256];
            {
                largerBuffer[124] = 0xD;
                largerBuffer[125] = 0xE;
                largerBuffer[126] = 0xE;
                largerBuffer[127] = 0xD;
            }

            using (var pub = new PublisherSocket())
            using (var sub = new SubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(100);

                pub.SendFrame(largerBuffer, 128);

                byte[] recvMessage = sub.ReceiveFrameBytes();

                Assert.Equal(128, recvMessage.Length);
                Assert.Equal(0xD, recvMessage[124]);
                Assert.Equal(0xE, recvMessage[125]);
                Assert.Equal(0xE, recvMessage[126]);
                Assert.Equal(0xD, recvMessage[127]);

                Assert.NotEqual(largerBuffer.Length, recvMessage.Length);
            }
        }

        [Fact]
        public void RawSocket()
        {
            using (var router = new RouterSocket())
            using (var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                router.Options.RouterRawSocket = true;
                var port = router.BindRandomPort("tcp://127.0.0.1");

                clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), port));
                clientSocket.NoDelay = true;

                byte[] clientMessage = Encoding.ASCII.GetBytes("HelloRaw");

                int bytesSent = clientSocket.Send(clientMessage);
                Assert.True(bytesSent > 0);

                byte[] id = router.ReceiveFrameBytes();
                byte[] message = router.ReceiveFrameBytes();

                router.SendMoreFrame(id).SendMoreFrame(message); // SendMore option is ignored

                var buffer = new byte[16];

                int bytesRead = clientSocket.Receive(buffer);
                Assert.True(bytesRead > 0);

                Assert.Equal(Encoding.ASCII.GetString(buffer, 0, bytesRead), "HelloRaw");
            }
        }

        [Fact]
        public void BindRandom()
        {
            using (var randomDealer = new DealerSocket())
            using (var connectingDealer = new DealerSocket())
            {
                int port = randomDealer.BindRandomPort("tcp://*");
                connectingDealer.Connect("tcp://127.0.0.1:" + port);

                randomDealer.SendFrame("test");

                Assert.Equal("test", connectingDealer.ReceiveFrameString());
            }
        }

        [Fact]
        public void BindToLocal()
        {
            var validAliasesForLocalHost = new[] { "127.0.0.1", "localhost", Dns.GetHostName() };

            foreach (var alias in validAliasesForLocalHost)
            {
                using (var localDealer = new DealerSocket())
                using (var connectingDealer = new DealerSocket())
                {
                    var port = localDealer.BindRandomPort("tcp://*");
                    connectingDealer.Connect($"tcp://{alias}:{port}");

                    localDealer.SendFrame("test");

                    Assert.Equal("test", connectingDealer.ReceiveFrameString());
                }
            }
        }

        [Fact, Trait("Category", "IPv6"), Trait("Category", "Explicit")]
        public void Ipv6ToIpv4()
        {
            using (var localDealer = new DealerSocket())
            using (NetMQSocket connectingDealer = new DealerSocket())
            {
                localDealer.Options.IPv4Only = false;
                var port = localDealer.BindRandomPort("tcp://*");

                connectingDealer.Connect($"tcp://{IPAddress.Loopback}:{port}");

                connectingDealer.SendFrame("test");

                Assert.Equal("test", localDealer.ReceiveFrameString());
            }
        }

        [Fact, Trait("Category", "IPv6"), Trait("Category", "Explicit")]
        public void Ipv6ToIpv6()
        {
            using (var localDealer = new DealerSocket())
            using (var connectingDealer = new DealerSocket())
            {
                localDealer.Options.IPv4Only = false;
                var port = localDealer.BindRandomPort("tcp://*");

                connectingDealer.Options.IPv4Only = false;
                connectingDealer.Connect($"tcp://{IPAddress.IPv6Loopback}:{port}");

                connectingDealer.SendFrame("test");

                Assert.Equal("test", localDealer.ReceiveFrameString());
            }
        }

        [Fact]
        public void HasInTest()
        {
            using (var server = new RouterSocket())
            using (var client = new DealerSocket())
            {
                var port = server.BindRandomPort("tcp://*");

                // no one sent a message so it should be false
                Assert.False(server.HasIn);

                client.Connect("tcp://localhost:" + port);

                // wait for the client to connect
                Thread.Sleep(100);

                // now we have one client connected but didn't send a message yet
                Assert.False(server.HasIn);

                client.SendFrame("1");

                // wait for the message to arrive
                Thread.Sleep(100);

                // the has in should indicate a message is ready
                Assert.True(server.HasIn);

                server.SkipFrame(); // identity
                string message = server.ReceiveFrameString();

                Assert.Equal(message, "1");

                // we read the message, it should false again
                Assert.False(server.HasIn);
            }
        }

        [Fact]
        public void DisposeImmediately()
        {
            using (var server = new DealerSocket())
            {
                server.BindRandomPort("tcp://*");
            }
        }

        [Fact]
        public void HasOutTest()
        {
            using (var server = new DealerSocket())
            {
                using (var client = new DealerSocket())
                {
                    var port = server.BindRandomPort("tcp://*");

                    // no client is connected so we don't have out
                    Assert.False(server.HasOut);

                    Assert.False(client.HasOut);

                    client.Connect("tcp://localhost:" + port);

                    Thread.Sleep(200);

                    // client is connected so server should have out now, client as well
                    Assert.True(server.HasOut);
                    Assert.True(client.HasOut);
                }

                //Thread.Sleep(2000);
                // client is disposed,server shouldn't have out now
                //Assert.False(server.HasOut);
            }
        }

        [Theory, InlineData("tcp"), InlineData("inproc")]
        public void Disconnect(string protocol)
        {
            using (var server1 = new DealerSocket())
            using (var server2 = new DealerSocket())
            using (var client = new DealerSocket())
            {
                string address2;

                if (protocol == "tcp")
                {
                    var port1 = server1.BindRandomPort("tcp://localhost");
                    var port2 = server2.BindRandomPort("tcp://localhost");

                    client.Connect("tcp://localhost:" + port1);
                    client.Connect("tcp://localhost:" + port2);

                    address2 = "tcp://localhost:" + port2;
                }
                else
                {
                    server1.Bind("inproc://localhost1");
                    server2.Bind("inproc://localhost2");

                    client.Connect("inproc://localhost1");
                    client.Connect("inproc://localhost2");

                    address2 = "inproc://localhost2";
                }

                Thread.Sleep(100);

                // we should be connected to both server
                client.SendFrame("1");
                client.SendFrame("2");

                // make sure client is connected to both servers
                server1.SkipFrame();
                server2.SkipFrame();

                // disconnect from server2, server 1 should receive all messages
                client.Disconnect(address2);
                Thread.Sleep(100);

                client.SendFrame("1");
                client.SendFrame("2");

                server1.SkipFrame();
                server1.SkipFrame();
            }
        }

        [Theory, InlineData("tcp"), InlineData("inproc")]
        public void Unbind(string protocol)
        {
            using (var server = new DealerSocket())
            {
                string address1, address2;

                // just making sure can bind on both addresses
                using (var client1 = new DealerSocket())
                using (var client2 = new DealerSocket())
                {
                    if (protocol == "tcp")
                    {
                        var port1 = server.BindRandomPort("tcp://localhost");
                        var port2 = server.BindRandomPort("tcp://localhost");

                        address1 = "tcp://localhost:" + port1;
                        address2 = "tcp://localhost:" + port2;

                        client1.Connect(address1);
                        client2.Connect(address2);
                    }
                    else
                    {
                        Debug.Assert(protocol == "inproc");

                        address1 = "inproc://localhost1";
                        address2 = "inproc://localhost2";

                        server.Bind(address1);
                        server.Bind(address2);

                        client1.Connect(address1);
                        client2.Connect(address2);
                    }

                    Thread.Sleep(100);

                    // we should be connected to both server
                    client1.SendFrame("1");
                    client2.SendFrame("2");

                    // the server receive from both
                    server.SkipFrame();
                    server.SkipFrame();
                }

                // unbind second address
                server.Unbind(address2);
                Thread.Sleep(100);

                using (var client1 = new DealerSocket())
                using (var client2 = new DealerSocket())
                {
                    client1.Options.DelayAttachOnConnect = true;
                    client1.Connect(address1);

                    client2.Options.DelayAttachOnConnect = true;

                    if (protocol == "tcp")
                    {
                        client2.Connect(address2);

                        client1.SendFrame("1");
                        server.SkipFrame();

                        Assert.False(client2.TrySendFrame(TimeSpan.FromSeconds(2), "2"));
                    }
                    else
                    {
                        Assert.Throws<EndpointNotFoundException>(() => { client2.Connect(address2); });
                    }
                }
            }
        }

        [Fact]
        public void BindRandomThenUnbind()
        {
            using (var pub = new PublisherSocket())
            {
                var port = pub.BindRandomPort("tcp://localhost");

                pub.Unbind("tcp://localhost:" + port);
            }

            using (var pub = new PublisherSocket())
            {
                var port = pub.BindRandomPort("tcp://*");

                pub.Unbind("tcp://*:" + port);
            }

            using (var pub = new PublisherSocket())
            {
                var port1 = pub.BindRandomPort("tcp://*");
                var port2 = pub.BindRandomPort("tcp://*");
                var port3 = pub.BindRandomPort("tcp://*");

                pub.Unbind("tcp://*:" + port1);
                pub.Unbind("tcp://*:" + port2);
                pub.Unbind("tcp://*:" + port3);
            }
        }

        [Fact]
        public void ReconnectOnRouterBug()
        {
            {
                using (var dealer = new DealerSocket())
                {
                    dealer.Options.Identity = Encoding.ASCII.GetBytes("dealer");
                    dealer.Bind("tcp://localhost:6667");

                    using (var router = new RouterSocket())
                    {
                        router.Options.RouterMandatory = true;
                        router.Connect("tcp://localhost:6667");
                        Thread.Sleep(100);

                        router.SendMoreFrame("dealer").SendFrame("Hello");
                        var message = dealer.ReceiveFrameString();
                        Assert.Equal("Hello", message);

                        router.Disconnect("tcp://localhost:6667");
                        Thread.Sleep(1000);
                        router.Connect("tcp://localhost:6667");
                        Thread.Sleep(100);

                        router.SendMoreFrame("dealer").SendFrame("Hello");
                        message = dealer.ReceiveFrameString();
                        Assert.Equal("Hello", message);
                    }
                }
            }
        }

        [Fact]
        public void RouterMandatoryTrueThrowsHostUnreachableException()
        {
            {
                using (var dealer = new DealerSocket())
                {
                    dealer.Options.Identity = Encoding.ASCII.GetBytes("dealer");
                    dealer.Bind("tcp://localhost:6667");

                    using (var router = new RouterSocket())
                    {
                        router.Options.RouterMandatory = true;
                        router.Connect("tcp://localhost:8889");

                        Assert.Throws<HostUnreachableException>(() => router.SendMoreFrame("dealer").SendFrame("Hello"));
                    }
                }
            }
        }


        [Fact]
        public void RouterMandatoryFalseDiscardsMessageSilently()
        {
            using (var dealer = new DealerSocket())
            {
                dealer.Options.Identity = Encoding.ASCII.GetBytes("dealer");
                dealer.Bind("tcp://localhost:6667");

                using (var router = new RouterSocket())
                {
                    router.Connect("tcp://localhost:8889");

                    router.SendMoreFrame("dealer").SendFrame("Hello");
                }
            }
        }

        [Fact]
        public void InprocRouterDealerTest()
        {
            // The main thread simply starts several clients and a server, and then
            // waits for the server to finish.
            var readyMsg = Encoding.UTF8.GetBytes("RDY");
            var freeWorkers = new Queue<byte[]>();

            using (var backendRouter = new RouterSocket())
            {
                backendRouter.Options.Identity = Guid.NewGuid().ToByteArray();
                backendRouter.Bind("inproc://backend");

                backendRouter.ReceiveReady += (o, e) =>
                {
                    // Handle worker activity on backend
                    while (e.Socket.HasIn)
                    {
                        var msg = e.Socket.ReceiveMultipartMessage();
                        var idRouter = msg.Pop();
                        // forget the empty frame
                        if (msg.First.IsEmpty)
                            msg.Pop();

                        var id = msg.Pop();
                        if (msg.First.IsEmpty)
                            msg.Pop();

                        if (msg.FrameCount == 1)
                        {
                            // worker send RDY message queue his Identity to the free workers queue
                            if (readyMsg[0] == msg[0].Buffer[0] &&
                                readyMsg[1] == msg[0].Buffer[1] &&
                                readyMsg[2] == msg[0].Buffer[2])
                            {
                                lock (freeWorkers)
                                {
                                    freeWorkers.Enqueue(id.Buffer);
                                }
                            }
                        }
                    }
                };

                using (var poller = new NetMQPoller {backendRouter})
                {
                    for (int i = 0; i < 2; i++)
                    {
                        void ThreadMethod(object state)
                        {
                            byte[] routerId = (byte[]) state;
                            byte[] workerId = Guid.NewGuid().ToByteArray();
                            using (var workerSocket = new DealerSocket())
                            {
                                workerSocket.Options.Identity = workerId;
                                workerSocket.Connect("inproc://backend");

                                var workerReadyMsg = new NetMQMessage();
                                workerReadyMsg.Append(workerId);
                                workerReadyMsg.AppendEmptyFrame();
                                workerReadyMsg.Append(readyMsg);
                                workerSocket.SendMultipartMessage(workerReadyMsg);
                                Thread.Sleep(1000);
                            }
                        }

                        var workerThread = new Thread(ThreadMethod)
                        {
                            IsBackground = true,
                            Name = "worker" + i
                        };

                        workerThread.Start(backendRouter.Options.Identity);
                    }

                    poller.RunAsync();
                    Thread.Sleep(1000);
                    poller.Stop();
                    Assert.Equal(2, freeWorkers.Count);
                }
            }
        }

        [Fact]
        public void ConnectionStringDefault()
        {
            using (var response = new ResponseSocket("tcp://127.0.0.1:51500"))
            using (var request = new RequestSocket("tcp://127.0.0.1:51500"))
            {
                request.SendFrame("Hello");

                Assert.Equal("Hello", response.ReceiveFrameString());
            }
        }

        [Fact]
        public void ConnectionStringSpecifyDefault()
        {
            using (var response = new ResponseSocket("@tcp://127.0.0.1:51501"))
            using (var request = new RequestSocket(">tcp://127.0.0.1:51501"))
            {
                request.SendFrame("Hello");

                Assert.Equal("Hello", response.ReceiveFrameString());
            }
        }

        [Fact]
        public void ConnectionStringSpecifyNonDefault()
        {
            using (var response = new ResponseSocket(">tcp://127.0.0.1:51502"))
            using (var request = new RequestSocket("@tcp://127.0.0.1:51502"))
            {
                request.SendFrame("Hello");

                Assert.Equal("Hello", response.ReceiveFrameString());
            }
        }

        [Fact]
        public void ConnectionStringWithWhiteSpace()
        {
            using (var response = new ResponseSocket(" >tcp://127.0.0.1:51503 "))
            using (var request = new RequestSocket("@tcp://127.0.0.1:51503, "))
            {
                request.SendFrame("Hello");

                Assert.Equal("Hello", response.ReceiveFrameString());
            }
        }

        [Fact]
        public void ConnectionStringMultipleAddresses()
        {
            using (var server1 = new DealerSocket("@tcp://127.0.0.1:51504"))
            using (var server2 = new DealerSocket("@tcp://127.0.0.1:51505,@tcp://127.0.0.1:51506"))
            using (var client = new DealerSocket("tcp://127.0.0.1:51504,tcp://127.0.0.1:51505,tcp://127.0.0.1:51506"))
            {
                // send three hello messages
                client.SendFrame("Hello");
                client.SendFrame("Hello");
                client.SendFrame("Hello");

                Assert.Equal("Hello", server1.ReceiveFrameString());
                Assert.Equal("Hello", server2.ReceiveFrameString());
                Assert.Equal("Hello", server2.ReceiveFrameString());
            }
        }

        [Fact]
        public void TestRebindSamePort()
        {
            int port;
            using (var response = new ResponseSocket())
            {
                port = response.BindRandomPort("tcp://127.0.0.1");
                response.Unbind();
            }
            using (var response = new ResponseSocket())
            {
                response.Bind($"tcp://127.0.0.1:{port}");
                response.Unbind();
            }
        }
    }

    internal static class NetMQSocketExtensions
    {
        private static long Counter;

        /// <summary>
        /// Unbind the socket from last endpoint and wait until the underlying socket was unbound and disposed.
        /// It will also dispose the NetMQSocket
        /// </summary>
        /// <param name="sub"></param>
        public static void Unbind(this NetMQSocket sub)
        {
            using (var monitor = new NetMQMonitor(sub, $"inproc://unbind.wait.{Counter++}", SocketEvents.Closed))
            {
                var monitorTask = Task.Factory.StartNew(monitor.Start);

                var closed = new ManualResetEventSlim();

                monitor.Closed += (sender, args) => closed.Set();

                Assert.NotNull(sub.Options.LastEndpoint);

                sub.Unbind(sub.Options.LastEndpoint);
                closed.Wait(1000);

                monitor.Stop();

                monitorTask.Wait();
            }
        }
    }
}
