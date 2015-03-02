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
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            {
                router.BindRandomPort("tcp://127.0.0.1");

                router.Receive(SendReceiveOptions.DontWait);
            }
        }

        [Test, ExpectedException(typeof(AgainException))]
        public void CheckSendAgainException()
        {
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            using (var dealer = context.CreateDealerSocket())
            {
                var port = router.BindRandomPort("tcp://127.0.0.1");
                router.Options.Linger = TimeSpan.Zero;

                dealer.Options.SendHighWatermark = 1;
                dealer.Options.Linger = TimeSpan.Zero;
                dealer.Connect("tcp://127.0.0.1:" + port);

                dealer.Send("1", true, false);
                dealer.Send("2", true, false);
            }
        }

        [Test]
        public void LargeMessage()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(100);

                var msg = new byte[300];

                pub.Send(msg);

                byte[] msg2 = sub.Receive();

                Assert.AreEqual(300, msg2.Length);
            }
        }

        [Test]
        public void ReceiveMessageWithTimeout()
        {
            using (var context = NetMQContext.Create())
            {
                var pubSync = new AutoResetEvent(false);
                var msg = new byte[300];
                const int waitTime = 500;

                var t1 = new Task(() =>
                {
                    using (var pubSocket = context.CreatePublisherSocket())
                    {
                        pubSocket.Bind("tcp://127.0.0.1:12345");
                        pubSync.WaitOne();
                        Thread.Sleep(waitTime);
                        pubSocket.Send(msg);
                        pubSync.WaitOne();
                    }
                }, TaskCreationOptions.LongRunning);

                var t2 = new Task(() =>
                {
                    using (var subSocket = context.CreateSubscriberSocket())
                    {
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
                    }
                }, TaskCreationOptions.LongRunning);

                t1.Start();
                t2.Start();

                Task.WaitAll(new[] { t1, t2 });
            }
        }

        [Test]
        public void LargeMessageLittleEndian()
        {
            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                pub.Options.Endian = Endianness.Little;
                var port = pub.BindRandomPort("tcp://127.0.0.1");

                sub.Options.Endian = Endianness.Little;
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(100);

                var msg = new byte[300];

                pub.Send(msg);

                byte[] msg2 = sub.Receive();

                Assert.AreEqual(300, msg2.Length);
            }
        }

        [Test]
        public void TestKeepAlive()
        {
            // there is no way to test tcp keep alive without disconnect the cable, we just testing that is not crashing the system
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                rep.Options.TcpKeepalive = true;
                rep.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                rep.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                req.Options.TcpKeepalive = true;
                req.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
                req.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

                var port = rep.BindRandomPort("tcp://127.0.0.1");
                req.Connect("tcp://127.0.0.1:" + port);

                req.Send("1");

                bool more;
                string m = rep.ReceiveString(out more);

                Assert.IsFalse(more);
                Assert.AreEqual("1", m);

                rep.Send("2");

                string m2 = req.ReceiveString(out more);

                Assert.IsFalse(more);
                Assert.AreEqual("2", m2);

                Assert.IsTrue(req.Options.TcpKeepalive);
                Assert.AreEqual(TimeSpan.FromSeconds(5), req.Options.TcpKeepaliveIdle);
                Assert.AreEqual(TimeSpan.FromSeconds(1), req.Options.TcpKeepaliveInterval);

                Assert.IsTrue(rep.Options.TcpKeepalive);
                Assert.AreEqual(TimeSpan.FromSeconds(5), rep.Options.TcpKeepaliveIdle);
                Assert.AreEqual(TimeSpan.FromSeconds(1), rep.Options.TcpKeepaliveInterval);
            }
        }

        [Test]
        public void MultipleLargeMessages()
        {
            var largeMessage = new byte[12000];

            for (int i = 0; i < 12000; i++)
            {
                largeMessage[i] = (byte)(i % 256);
            }

            using (var context = NetMQContext.Create())
            using (var pub = context.CreatePublisherSocket())
            using (var sub = context.CreateSubscriberSocket())
            {
                var port = pub.BindRandomPort("tcp://127.0.0.1");
                sub.Connect("tcp://127.0.0.1:" + port);
                sub.Subscribe("");

                Thread.Sleep(1000);

                pub.Send("");
                sub.Receive();

                for (int i = 0; i < 100; i++)
                {
                    pub.Send(largeMessage);

                    byte[] recvMesage = sub.Receive();

                    for (int j = 0; j < 12000; j++)
                    {
                        Assert.AreEqual(largeMessage[j], recvMesage[j]);
                    }
                }
            }
        }

        [Test]
        public void RawSocket()
        {
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            using (var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                router.Options.RouterRawSocket = true;
                var port = router.BindRandomPort("tcp://127.0.0.1");

                clientSocket.Connect("127.0.0.1", port);
                clientSocket.NoDelay = true;

                byte[] clientMessage = Encoding.ASCII.GetBytes("HelloRaw");

                int bytesSent = clientSocket.Send(clientMessage);
                Assert.Greater(bytesSent, 0);

                byte[] id = router.Receive();
                byte[] message = router.Receive();

                router.SendMore(id).SendMore(message); // SNDMORE option is ignored

                var buffer = new byte[16];

                int bytesRead = clientSocket.Receive(buffer);
                Assert.Greater(bytesRead, 0);

                Assert.AreEqual(Encoding.ASCII.GetString(buffer, 0, bytesRead), "HelloRaw");
            }
        }

        [Test]
        public void BindRandom()
        {
            using (var context = NetMQContext.Create())
            using (var randomDealer = context.CreateDealerSocket())
            using (var connectingDealer = context.CreateDealerSocket())
            {
                int port = randomDealer.BindRandomPort("tcp://*");
                connectingDealer.Connect("tcp://127.0.0.1:" + port);

                randomDealer.Send("test");

                Assert.AreEqual("test", connectingDealer.ReceiveString());
            }
        }

        [Test]
        public void BindToLocal()
        {
            var validAliasesForLocalHost = new[] { "127.0.0.1", "localhost", Dns.GetHostName() };
            
            foreach (var alias in validAliasesForLocalHost)
            {
                using (var context = NetMQContext.Create())
                using (var localDealer = context.CreateDealerSocket())
                using (var connectingDealer = context.CreateDealerSocket())
                {
                    var port = localDealer.BindRandomPort("tcp://*");
                    connectingDealer.Connect(string.Format("tcp://{0}:{1}", alias, port));

                    localDealer.Send("test");

                    Assert.AreEqual("test", connectingDealer.ReceiveString());
                    Console.WriteLine(alias + " connected ");
                }
            }
        }

        [Test, Category("IPv6")]
        public void Ipv6ToIpv4()
        {
            using (var context = NetMQContext.Create())
            using (var localDealer = context.CreateDealerSocket())
            using (NetMQSocket connectingDealer = context.CreateDealerSocket())
            {
                localDealer.Options.IPv4Only = false;
                var port = localDealer.BindRandomPort(string.Format("tcp://*"));

                connectingDealer.Connect(string.Format("tcp://{0}:{1}", IPAddress.Loopback, port));

                connectingDealer.Send("test");

                Assert.AreEqual("test", localDealer.ReceiveString());
            }
        }

        [Test, Category("IPv6")]
        public void Ipv6ToIpv6()
        {
            using (var context = NetMQContext.Create())
            using (var localDealer = context.CreateDealerSocket())
            using (var connectingDealer = context.CreateDealerSocket())
            {
                localDealer.Options.IPv4Only = false;
                var port = localDealer.BindRandomPort(string.Format("tcp://*"));

                connectingDealer.Options.IPv4Only = false;
                connectingDealer.Connect(string.Format("tcp://{0}:{1}", IPAddress.IPv6Loopback, port));

                connectingDealer.Send("test");

                Assert.AreEqual("test", localDealer.ReceiveString());
            }
        }

        [Test]
        public void HasInTest()
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateRouterSocket())
            using (var client = context.CreateDealerSocket())
            {
                var port = server.BindRandomPort("tcp://*");

                // no one sent a message so it should be fasle
                Assert.IsFalse(server.HasIn);

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

                server.Receive(); // identity
                string message = server.ReceiveString();

                Assert.AreEqual(message, "1");

                // we read the message, it should false again
                Assert.IsFalse(server.HasIn);
            }
        }

        [Test]
        public void DisposeImmediately()
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateDealerSocket())
            {
                server.BindRandomPort("tcp://*");
            }
        }

        [Test]
        public void HasOutTest()
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateDealerSocket())
            {
                using (var client = context.CreateDealerSocket())
                {
                    var port = server.BindRandomPort("tcp://*");

                    // no client is connected so we don't have out
                    Assert.IsFalse(server.HasOut);

                    Assert.IsFalse(client.HasOut);

                    client.Connect("tcp://localhost:" + port);

                    Thread.Sleep(200);

                    // client is connected so server should have out now, client as well
                    Assert.IsTrue(server.HasOut);
                    Assert.IsTrue(client.HasOut);
                }

                //Thread.Sleep(2000);
                // client is disposed,server shouldn't have out now
                //Assert.IsFalse(server.HasOut);
            }
        }

        [Test, TestCase("tcp"), TestCase("inproc")]
        public void Disconnect(string protocol)
        {
            using (var context = NetMQContext.Create())
            using (var server1 = context.CreateDealerSocket())
            using (var server2 = context.CreateDealerSocket())
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

                // we should be connected to both server
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

        [Test, TestCase("tcp"), TestCase("inproc")]
        public void Unbind(string protocol)
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateDealerSocket())
            {
                string address1, address2;

                // just making sure can bind on both adddresses
                using (var client1 = context.CreateDealerSocket())
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

                    // we should be connected to both server
                    client1.Send("1");
                    client2.Send("2");

                    // the server receive from both
                    server.ReceiveString();
                    server.ReceiveString();
                }

                // unbind second address
                server.Unbind(address2);
                Thread.Sleep(100);

                using (var client1 = context.CreateDealerSocket())
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
                        Assert.Throws<EndpointNotFoundException>(() => { client2.Connect(address2); });
                    }
                }
            }
        }

        [Test]
        public void ASubscriberSocketThatGetDisconnectedBlocksItsContextFromBeingDisposed()
        {
            // NOTE two contexts here

            using (var subContext = NetMQContext.Create())
            using (var pubContext = NetMQContext.Create())
            using (var pub = pubContext.CreatePublisherSocket())
            using (var sub = subContext.CreateSubscriberSocket())
            {
                pub.Options.Linger = TimeSpan.FromSeconds(0);
                pub.Options.SendTimeout = TimeSpan.FromSeconds(2);

                sub.Options.Linger = TimeSpan.FromSeconds(0);
            
                sub.Connect("tcp://localhost:12345");
                sub.Subscribe("");
                
//                Thread.Sleep(1000);

                pub.Bind("tcp://localhost:12345");

                // NOTE the test fails if you remove this sleep
                Thread.Sleep(1000);

                for (var i = 0; i < 100; i++)
                {
                    var msg = "msg-" + i;
                    pub.Send("msg-" + i);
                    Assert.AreEqual(msg, sub.ReceiveString(TimeSpan.FromMilliseconds(100)));
                }

                pub.Close();

//                Thread.Sleep(1000);
            }
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
