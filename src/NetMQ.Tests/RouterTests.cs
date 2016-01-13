using System;
using NUnit.Framework;
using System.Text;
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
                router.BindRandomPort("tcp://*");

                Assert.Throws<HostUnreachableException>(() => router.SendMoreFrame("UNKNOWN").SendFrame("Hello"));
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
    }
}
