using System;
using NUnit.Framework;
using System.Text;
using System.Threading;
using NetMQ.Sockets;

// ReSharper disable AccessToDisposedClosure

namespace NetMQ.Tests
{
    [TestFixture]
    public class RouterTests
    {
        [Test]
        public void Mandatory()
        {
            using (var router = new RouterSocket())
            {
                router.Options.RouterMandatory = true;
                router.Bind("tcp://127.0.0.1:5555");

                using (var dealer = new DealerSocket())
                {
                    dealer.Options.Identity = Encoding.ASCII.GetBytes("1");
                    dealer.Connect("tcp://127.0.0.1:5555");

                    dealer.SendFrame("Hello");

                    Assert.AreEqual("1", router.ReceiveFrameString());
                    Assert.AreEqual("Hello", router.ReceiveFrameString());
                }

                Thread.Sleep(100);

                Assert.Throws<HostUnreachableException>(() => router.SendMoreFrame("1").SendFrame("Hello"));
            }
            
        }

        [Test]
        public void ReceiveReadyDot35Bug()
        {
            // In .NET 3.5, we saw an issue where ReceiveReady would be raised every second despite nothing being received
            using (var server = new RouterSocket())
            {
                server.BindRandomPort("tcp://127.0.0.1");
                server.ReceiveReady += (s, e) => Assert.Fail("Should not receive");

                Assert.IsFalse(server.Poll(TimeSpan.FromMilliseconds(1500)));
            }
        }

        [Test]
        public void TwoMessagesFromRouterToDealer()
        {
            using (var server = new RouterSocket())
            using (var client = new DealerSocket())
            using (var poller = new NetMQPoller { client })
            {
                var port = server.BindRandomPort("tcp://*");
                client.Connect("tcp://127.0.0.1:" + port);
                var cnt = 0;
                client.ReceiveReady += (sender, e) =>
                {
                    var strs = e.Socket.ReceiveMultipartStrings();
                    foreach (var str in strs)
                    {
                        Console.WriteLine(str);
                    }
                    cnt++;
                    if (cnt == 2)
                    {
                        poller.Stop();
                    }
                };
                byte[] clientId = Encoding.Unicode.GetBytes("ClientId");
                client.Options.Identity = clientId;

                const string request = "GET /\r\n";

                const string response = "HTTP/1.0 200 OK\r\n" +
                        "Content-Type: text/plain\r\n" +
                        "\r\n" +
                        "Hello, World!";

                client.SendFrame(request);

                byte[] serverId = server.ReceiveFrameBytes();
                Assert.AreEqual(request, server.ReceiveFrameString());

                // two messages in a row, not frames
                server.SendMoreFrame(serverId).SendFrame(response);
                server.SendMoreFrame(serverId).SendFrame(response);

                poller.Run();
            }
        }

        [Test]
        public void Handover()
        {
            using (var router = new RouterSocket())
            using (var dealer1 = new DealerSocket())
            {
                router.Options.RouterHandover = true;
                router.Bind("inproc://127.0.0.1:5555");
                dealer1.Options.Identity = Encoding.ASCII.GetBytes("ID");
                dealer1.Connect("inproc://127.0.0.1:5555");
                dealer1.SendMoreFrame("Hello").SendFrame("World");

                var identity = router.ReceiveFrameString();
                Assert.AreEqual("ID", identity);

                using (var dealer2 = new DealerSocket())
                {
                    dealer2.Options.Identity = Encoding.ASCII.GetBytes("ID");
                    dealer2.Connect("inproc://127.0.0.1:5555");

                    // We have new peer which should take over, however we are still reading a message                    
                    var message = router.ReceiveFrameString();
                    Assert.AreEqual("Hello", message);
                    message = router.ReceiveFrameString();
                    Assert.AreEqual("World", message);

                    dealer2.SendMoreFrame("Hello").SendFrame("World");
                    identity = router.ReceiveFrameString();
                    Assert.AreEqual("ID", identity);

                    message = router.ReceiveFrameString();
                    Assert.AreEqual("Hello", message);

                    message = router.ReceiveFrameString();
                    Assert.AreEqual("World", message);
                }
            }
        }
    }
}
