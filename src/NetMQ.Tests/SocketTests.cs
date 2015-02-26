using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class SocketTests
    {
        [Test, ExpectedException(typeof(AgainException))]
        public void CheckRecvAgainException()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var routerSocket = context.CreateRouterSocket())
                {
                    routerSocket.BindRandomPort("tcp://127.0.0.1");

                    routerSocket.Receive(SendReceiveOptions.DontWait);
                }
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
        public void CheckSendAgainException()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var routerSocket = context.CreateRouterSocket())
                {
                    var port = routerSocket.BindRandomPort("tcp://127.0.0.1");
                    routerSocket.Options.Linger = TimeSpan.Zero;

                    using (var dealerSocket = context.CreateDealerSocket())
                    {
                        dealerSocket.Options.SendHighWatermark = 1;
                        dealerSocket.Options.Linger = TimeSpan.Zero;
                        dealerSocket.Connect("tcp://127.0.0.1:" + port);

                        dealerSocket.Send("1", true, false);
                        dealerSocket.Send("2", true, false);
                    }
                }
            }
        }

        [Test]
        public void LargeMessage()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pubSocket = context.CreatePublisherSocket())
                {
                    var port = pubSocket.BindRandomPort("tcp://127.0.0.1");

                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Connect("tcp://127.0.0.1:" + port);
                        subSocket.Subscribe("");

                        Thread.Sleep(100);

                        byte[] msg = new byte[300];

                        pubSocket.Send(msg);

                        byte[] msg2 = subSocket.Receive();

                        Assert.AreEqual(300, msg2.Length);
                    }
                }
            }
        }

        [Test]
        public void ReceiveMessageWithTimeout()
        {
            using (var context = NetMQContext.Create())
            {
                var pubSync = new AutoResetEvent(false);
                var msg = new byte[300];
                var waitTime = 500;

                var t1 = new Task(() =>
                {
                    var pubSocket = context.CreatePublisherSocket();
                    pubSocket.Bind("tcp://127.0.0.1:12345");
                    pubSync.WaitOne();
                    Thread.Sleep(waitTime);
                    pubSocket.Send(msg);
                    pubSync.WaitOne();
                    pubSocket.Dispose();

                }, TaskCreationOptions.LongRunning);

                var t2 = new Task(() =>
                {
                    var subSocket = context.CreateSubscriberSocket();
                    subSocket.Connect("tcp://127.0.0.1:12345");
                    subSocket.Subscribe("");
                    Thread.Sleep(100);
                    pubSync.Set();

                    var msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(100));
                    Assert.IsNull(msg2, "The first receive should be null!");

                    msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(waitTime));

                    Assert.NotNull(msg2);
                    Assert.AreEqual(1, msg2.FrameCount);
                    Assert.AreEqual(300, msg2.First.MessageSize);
                    pubSync.Set();
                    subSocket.Dispose();
                }, TaskCreationOptions.LongRunning);

                t1.Start();
                t2.Start();

                Task.WaitAll(new[] { t1, t2 });
            }
        }

        [Test]
        public void LargeMessageLittleEndian()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var pubSocket = context.CreatePublisherSocket())
                {
                    pubSocket.Options.Endian = Endianness.Little;
                    var port = pubSocket.BindRandomPort("tcp://127.0.0.1");

                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Options.Endian = Endianness.Little;
                        subSocket.Connect("tcp://127.0.0.1:" + port);
                        subSocket.Subscribe("");

                        Thread.Sleep(100);

                        byte[] msg = new byte[300];

                        pubSocket.Send(msg);

                        byte[] msg2 = subSocket.Receive();

                        Assert.AreEqual(300, msg2.Length);
                    }
                }
            }
        }

        [Test]
        public void TestKeepAlive()
        {
            // there is no way to test tcp keep alive without disconnect the cable, we just testing that is not crashing the system
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var responseSocket = context.CreateResponseSocket())
                {
                    responseSocket.Options.TcpKeepalive = true;
                    responseSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                    responseSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                    var port = responseSocket.BindRandomPort("tcp://127.0.0.1");

                    using (var requestSocket = context.CreateRequestSocket())
                    {
                        requestSocket.Options.TcpKeepalive = true;
                        requestSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                        requestSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                        requestSocket.Connect("tcp://127.0.0.1:" + port);

                        requestSocket.Send("1");

                        bool more;
                        string m = responseSocket.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("1", m);

                        responseSocket.Send("2");

                        string m2 = requestSocket.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("2", m2);

                        Assert.IsTrue(requestSocket.Options.TcpKeepalive);
                        Assert.AreEqual(TimeSpan.FromSeconds(5), requestSocket.Options.TcpKeepaliveIdle);
                        Assert.AreEqual(TimeSpan.FromSeconds(1), requestSocket.Options.TcpKeepaliveInterval);

                        Assert.IsTrue(responseSocket.Options.TcpKeepalive);
                        Assert.AreEqual(TimeSpan.FromSeconds(5), responseSocket.Options.TcpKeepaliveIdle);
                        Assert.AreEqual(TimeSpan.FromSeconds(1), responseSocket.Options.TcpKeepaliveInterval);
                    }
                }
            }
        }

        [Test]
        public void MultipleLargeMessages()
        {
            byte[] largeMessage = new byte[12000];

            for (int i = 0; i < 12000; i++)
            {
                largeMessage[i] = (byte)(i % 256);
            }

            using (NetMQContext context = NetMQContext.Create())
            {
                using (PublisherSocket pubSocket = context.CreatePublisherSocket())
                {
                    var port = pubSocket.BindRandomPort("tcp://127.0.0.1");

                    using (SubscriberSocket subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Connect("tcp://127.0.0.1:" + port);
                        subSocket.Subscribe("");

                        Thread.Sleep(1000);

                        pubSocket.Send("");
                        subSocket.Receive();

                        for (int i = 0; i < 100; i++)
                        {
                            pubSocket.Send(largeMessage);

                            byte[] recvMesage = subSocket.Receive();

                            for (int j = 0; j < 12000; j++)
                            {
                                Assert.AreEqual(largeMessage[j], recvMesage[j]);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void RawSocket()
        {
            byte[] message;
            byte[] id;

            using (NetMQContext context = NetMQContext.Create())
            {
                using (var routerSocket = context.CreateRouterSocket())
                {
                    routerSocket.Options.RouterRawSocket = true;
                    var port = routerSocket.BindRandomPort("tcp://127.0.0.1");

                    using (var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        clientSocket.Connect("127.0.0.1", port);
                        clientSocket.NoDelay = true;

                        byte[] clientMessage = Encoding.ASCII.GetBytes("HelloRaw");

                        int bytesSent = clientSocket.Send(clientMessage);
                        Assert.Greater(bytesSent, 0);

                        id = routerSocket.Receive();
                        message = routerSocket.Receive();

                        routerSocket.SendMore(id).
                          SendMore(message); // SNDMORE option is ignored

                        byte[] buffer = new byte[16];

                        int bytesRead = clientSocket.Receive(buffer);
                        Assert.Greater(bytesRead, 0);

                        Assert.AreEqual(Encoding.ASCII.GetString(buffer, 0, bytesRead), "HelloRaw");
                    }
                }
            }
        }

        [Test]
        public void BindRandom()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket randomDealer = context.CreateDealerSocket())
                {
                    int port = randomDealer.BindRandomPort("tcp://*");

                    using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                    {
                        connectingDealer.Connect("tcp://127.0.0.1:" + port);

                        randomDealer.Send("test");

                        Assert.AreEqual("test", connectingDealer.ReceiveString());
                    }
                }
            }
        }

        [Test]
        public void BindToLocal()
        {
            var validAliasesForLocalHost = new[] { "127.0.0.1", "localhost", System.Net.Dns.GetHostName() };
            foreach (var alias in validAliasesForLocalHost)
            {
                using (NetMQContext context = NetMQContext.Create())
                {
                    using (NetMQSocket localDealer = context.CreateDealerSocket())
                    {
                        var port = localDealer.BindRandomPort("tcp://*");

                        using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                        {
                            connectingDealer.Connect(string.Format("tcp://{0}:{1}", alias, port));

                            localDealer.Send("test");

                            Assert.AreEqual("test", connectingDealer.ReceiveString());
                            Console.WriteLine(alias + " connected ");
                        }
                    }
                }
            }
        }

        [Test, Category("IPv6")]
        public void Ipv6ToIpv4()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket localDealer = context.CreateDealerSocket())
                {
                    localDealer.Options.IPv4Only = false;
                    var port = localDealer.BindRandomPort(string.Format("tcp://*"));
                    using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                    {
                        connectingDealer.Connect(string.Format("tcp://{0}:{1}", IPAddress.Loopback, port));

                        connectingDealer.Send("test");

                        Assert.AreEqual("test", localDealer.ReceiveString());
                    }
                }
            }
        }

        [Test, Category("IPv6")]
        public void Ipv6ToIpv6()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket localDealer = context.CreateDealerSocket())
                {
                    localDealer.Options.IPv4Only = false;
                    var port = localDealer.BindRandomPort(string.Format("tcp://*"));

                    using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                    {
                        connectingDealer.Options.IPv4Only = false;
                        connectingDealer.Connect(string.Format("tcp://{0}:{1}", IPAddress.IPv6Loopback, port));

                        connectingDealer.Send("test");

                        Assert.AreEqual("test", localDealer.ReceiveString());
                    }
                }
            }
        }

        [Test]
        public void HasInTest()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket server = context.CreateRouterSocket())
                {
                    var port = server.BindRandomPort("tcp://*");

                    // no one sent a message so it should be fasle
                    Assert.IsFalse(server.HasIn);

                    using (NetMQSocket client = context.CreateDealerSocket())
                    {
                        client.Connect("tcp://localhost:" + port);

                        // wait for the client to connect
                        Thread.Sleep(100);

                        // now we have one client connected but didn't send a message yet
                        Assert.IsFalse(server.HasIn);

                        client.Send("1");

                        // wait for the message to arrive
                        Thread.Sleep(100);

                        // the has in should indicate a message is ready
                        Assert.IsTrue(server.HasIn);

                        byte[] identity = server.Receive();
                        string message = server.ReceiveString();

                        Assert.AreEqual(message, "1");

                        // we read the message, it should false again
                        Assert.IsFalse(server.HasIn);
                    }
                }
            }
        }

        [Test]
        public void DisposeImmediatly()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket server = context.CreateDealerSocket())
                {
                    server.BindRandomPort("tcp://*");
                }
            }
        }

        [Test]
        public void HasOutTest()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket server = context.CreateDealerSocket())
                {
                    var port = server.BindRandomPort("tcp://*");

                    // no client is connected so we don't have out
                    Assert.IsFalse(server.HasOut);

                    using (NetMQSocket client = context.CreateDealerSocket())
                    {
                        Assert.IsFalse(client.HasOut);

                        client.Connect("tcp://localhost:" + port);

                        Thread.Sleep(200);

                        // client is connected so server should have out now, client as well
                        Assert.IsTrue(server.HasOut);
                        Assert.IsTrue(client.HasOut);
                    }

                    Thread.Sleep(2000);

                    // client is disposed,server shouldn't have out now
                    //Assert.IsFalse(server.HasOut);
                }
            }
        }

        [Test, TestCase("tcp"), TestCase("inproc")]
        public void Disconnect(string protocol)
        {
            using (var context = NetMQContext.Create())
            {
                using (var server1 = context.CreateDealerSocket())
                {
                    using (var server2 = context.CreateDealerSocket())
                    {
                        using (var client = context.CreateDealerSocket())
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

                            // we shoud be connected to both server
                            client.Send("1");
                            client.Send("2");

                            // make sure client is connected to both servers 
                            server1.ReceiveString();
                            server2.ReceiveString();

                            // disconnect from server2, server 1 should receive all messages
                            client.Disconnect(address2);
                            Thread.Sleep(100);

                            client.Send("1");
                            client.Send("2");

                            server1.ReceiveString();
                            server1.ReceiveString();
                        }
                    }
                }
            }
        }

        [Test, TestCase("tcp"), TestCase("inproc")]
        public void Unbind(string protocol)
        {
            using (var context = NetMQContext.Create())
            {
                using (var server = context.CreateDealerSocket())
                {
                    string address1, address2;

                    // just making sure can bind on both adddresses
                    using (var client1 = context.CreateDealerSocket())
                    {
                        using (var client2 = context.CreateDealerSocket())
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

                            // we shoud be connected to both server
                            client1.Send("1");
                            client2.Send("2");

                            // the server receive from both
                            server.ReceiveString();
                            server.ReceiveString();
                        }
                    }

                    // unbind second address
                    server.Unbind(address2);
                    Thread.Sleep(100);

                    using (var client1 = context.CreateDealerSocket())
                    {
                        using (var client2 = context.CreateDealerSocket())
                        {
                            client1.Options.DelayAttachOnConnect = true;
                            client1.Connect(address1);

                            client2.Options.SendTimeout = TimeSpan.FromSeconds(2);
                            client2.Options.DelayAttachOnConnect = true;

                            if (protocol == "tcp")
                            {
                                client2.Connect(address2);

                                client1.Send("1");
                                server.ReceiveString();

                                Assert.Throws<AgainException>(() =>
                                {
                                    // this should raise exception
                                    client2.Send("2");
                                });
                            }
                            else
                            {
                                var exception = Assert.Throws<EndpointNotFoundException>(() =>
                                {
                                    client2.Connect(address2);
                                });
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void ASubscriberSocketThatGetDisconnectedBlockItsContextFromBeingDisposed()
        {
            using (var subscriberCtx = NetMQContext.Create())
            {
                using (var publisherCtx = NetMQContext.Create())
                {
                    using (var pubSocket = publisherCtx.CreatePublisherSocket())
                    using (var subSocket = subscriberCtx.CreateSubscriberSocket())
                    {

                        pubSocket.Options.Linger = TimeSpan.FromSeconds(0);
                        pubSocket.Options.SendTimeout = TimeSpan.FromSeconds(2);

                        subSocket.Options.Linger = TimeSpan.FromSeconds(0);
                        subSocket.Connect("tcp://localhost:12345");
                        subSocket.Subscribe("");
                        Debug.WriteLine("Subscriber socket connecting...");
                        Thread.Sleep(2000);

                        Debug.WriteLine("Publisher socket binding...");
                        pubSocket.Bind("tcp://localhost:12345");

                        Thread.Sleep(2000);

                        for (var i = 0; i < 100; i++)
                        {
                            var msg = "msg-" + i;
                            pubSocket.Send("msg-" + i);
                            var recvMsg = subSocket.ReceiveString();
                            Assert.AreEqual(recvMsg, msg);
                        }
                        Debug.WriteLine("Sockets exchanged messages.");

                        pubSocket.Close();

                        Thread.Sleep(1000);
                    }
                    Debug.WriteLine("Sockets disposed.");
                }
                Debug.WriteLine("Publisher ctx disposed.");
            }
            Debug.WriteLine("Subscriber ctx disposed.");
        }        

        [Test]
        public void BindRandomThenUnbind()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            {
                var port = pub.BindRandomPort("tcp://localhost");

                pub.Unbind("tcp://localhost:" + port);
            }
            
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            {
                var port = pub.BindRandomPort("tcp://*");

                pub.Unbind("tcp://*:" + port);
            }
            
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            {
                var port1 = pub.BindRandomPort("tcp://*");
                var port2 = pub.BindRandomPort("tcp://*");
                var port3 = pub.BindRandomPort("tcp://*");

                pub.Unbind("tcp://*:" + port1);
                pub.Unbind("tcp://*:" + port2);
                pub.Unbind("tcp://*:" + port3);
            }
        }
    }
}
