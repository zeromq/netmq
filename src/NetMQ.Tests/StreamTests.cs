using NUnit.Framework;

using NetMQ.Sockets;

namespace NetMQ.Tests
{
    [TestFixture]
    public class StreamTests
    {
        [Test]
        public void StreamToStream()
        {
            using (var server = new StreamSocket())
            using (var client = new StreamSocket())
            {
                var port = server.BindRandomPort("tcp://*");
                client.Connect("tcp://127.0.0.1:" + port);

                byte[] clientId = client.Options.Identity;

                const string request = "GET /\r\n";

                const string response = "HTTP/1.0 200 OK\r\n" +
                        "Content-Type: text/plain\r\n" +
                        "\r\n" +
                        "Hello, World!";

                client.SendMoreFrame(clientId).SendFrame(request);

                byte[] serverId = server.ReceiveFrameBytes();
                Assert.AreEqual(request, server.ReceiveFrameString());

                server.SendMoreFrame(serverId).SendFrame(response);

                CollectionAssert.AreEqual(clientId, client.ReceiveFrameBytes());
                Assert.AreEqual(response, client.ReceiveFrameString());
            }
        }
    }
}
