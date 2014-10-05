using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Sockets;
using NetMQ.zmq;

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
                    routerSocket.Bind("tcp://127.0.0.1:5555");

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
                    routerSocket.Bind("tcp://127.0.0.1:5555");
                    routerSocket.Options.Linger = TimeSpan.Zero;

                    using (var dealerSocket = context.CreateDealerSocket())
                    {
                        dealerSocket.Options.SendHighWatermark = 1;
                        dealerSocket.Options.Linger = TimeSpan.Zero;
                        dealerSocket.Connect("tcp://127.0.0.1:5555");

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
                    pubSocket.Bind("tcp://127.0.0.1:5556");

                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Connect("tcp://127.0.0.1:5556");
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
                    pubSocket.Bind("tcp://127.0.0.1:5556");
                    pubSync.WaitOne();
                    Thread.Sleep(waitTime);
                    pubSocket.Send(msg);
                    pubSync.WaitOne();
                    pubSocket.Dispose();
                }, TaskCreationOptions.LongRunning);

                var t2 = new Task(() =>
                {
                    var subSocket = context.CreateSubscriberSocket();
                    subSocket.Connect("tcp://127.0.0.1:5556");
                    subSocket.Subscribe("");
                    Thread.Sleep(100);
                    pubSync.Set();

                    var msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(10));
                    Assert.IsNull(msg2, "The first receive should be null!");

                    msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(waitTime));

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
                    pubSocket.Bind("tcp://127.0.0.1:5556");

                    using (var subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Options.Endian = Endianness.Little;
                        subSocket.Connect("tcp://127.0.0.1:5556");
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

                    responseSocket.Bind("tcp://127.0.0.1:5555");

                    using (var requestSocket = context.CreateRequestSocket())
                    {
                        requestSocket.Options.TcpKeepalive = true;
                        requestSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                        requestSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                        requestSocket.Connect("tcp://127.0.0.1:5555");

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
                using (NetMQSocket pubSocket = context.CreatePublisherSocket())
                {
                    pubSocket.Bind("tcp://127.0.0.1:5558");

                    using (NetMQSocket subSocket = context.CreateSubscriberSocket())
                    {
                        subSocket.Connect("tcp://127.0.0.1:5558");
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
                    routerSocket.Bind("tcp://127.0.0.1:5556");

                    using (var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        clientSocket.Connect("127.0.0.1", 5556);
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
                        localDealer.Bind("tcp://*:5002");

                        using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                        {
                            connectingDealer.Connect("tcp://" + alias + ":5002");

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
                    localDealer.Bind(string.Format("tcp://*:5002"));
                    using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                    {
                        connectingDealer.Connect("tcp://" + IPAddress.Loopback.ToString() + ":5002");

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
                    localDealer.Bind(string.Format("tcp://*:5002"));

                    using (NetMQSocket connectingDealer = context.CreateDealerSocket())
                    {
                        connectingDealer.Options.IPv4Only = false;
                        connectingDealer.Connect("tcp://" + IPAddress.IPv6Loopback.ToString() + ":5002");

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
                    server.Bind("tcp://*:5557");

                    // no one sent a message so it should be fasle
                    Assert.IsFalse(server.HasIn);

                    using (NetMQSocket client = context.CreateDealerSocket())
                    {
                        client.Connect("tcp://localhost:5557");

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
        public void HasOutTest()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket server = context.CreateDealerSocket())
                {
                    server.Bind("tcp://*:5557");

                    // no client is connected so we don't have out
                    Assert.IsFalse(server.HasOut);

                    using (NetMQSocket client = context.CreateDealerSocket())
                    {
                        Assert.IsFalse(client.HasOut);

                        client.Connect("tcp://localhost:5557");

                        Thread.Sleep(100);

                        // client is connected so server should have out now, client as well
                        Assert.IsTrue(server.HasOut);
                        Assert.IsTrue(client.HasOut);
                    }

                    Thread.Sleep(100);

                    // client is disposed,server shouldn't have out now
                    Assert.IsFalse(server.HasOut);
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
                            server1.Bind(protocol + "://localhost:55502");
                            server2.Bind(protocol + "://localhost:55503");

                            client.Connect(protocol + "://localhost:55502");
                            client.Connect(protocol + "://localhost:55503");

                            Thread.Sleep(100);

                            // we shoud be connected to both server
                            client.Send("1");
                            client.Send("2");

                            // make sure client is connected to both servers 
                            server1.ReceiveString();
                            server2.ReceiveString();

                            // disconnect from server2, server 1 should receive all messages
                            client.Disconnect(protocol + "://localhost:55503");
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
                    server.Bind(protocol + "://localhost:55502");
                    server.Bind(protocol + "://localhost:55503");

                    // just making sure can bind on both adddresses
                    using (var client1 = context.CreateDealerSocket())
                    {
                        using (var client2 = context.CreateDealerSocket())
                        {
                            client1.Connect(protocol + "://localhost:55502");
                            client2.Connect(protocol + "://localhost:55503");

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
                    server.Unbind(protocol + "://localhost:55503");
                    Thread.Sleep(100);

                    using (var client1 = context.CreateDealerSocket())
                    {
                        using (var client2 = context.CreateDealerSocket())
                        {
                            client1.Options.DelayAttachOnConnect = true;
                            client1.Connect(protocol + "://localhost:55502");

                            client2.Options.SendTimeout = TimeSpan.FromSeconds(2);
                            client2.Options.DelayAttachOnConnect = true;

                            if (protocol == "tcp")
                            {
                                client2.Connect(protocol + "://localhost:55503");

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
                                var exception = Assert.Throws<InvalidException>(() =>
                                {
                                    client2.Connect(protocol + "://localhost:55503");
                                });                                
                            }                                                    
                        }
                    }
                }
            }
        }
    }
}
