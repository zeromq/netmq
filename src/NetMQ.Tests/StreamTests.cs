using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class StreamTests
    {
        [Test]
        public void StreamToStream()
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateStreamSocket())
            using (var client = context.CreateStreamSocket())
            {
                var port = server.BindRandomPort("tcp://*");
                client.Connect("tcp://127.0.0.1:" + port);

                byte[] clientId = client.Options.Identity;

                const string request = "GET /\r\n";

                const string response = "HTTP/1.0 200 OK\r\n" +
                        "Content-Type: text/plain\r\n" +
                        "\r\n" +
                        "Hello, World!";

                client.SendMore(clientId).Send(request);

                byte[] serverId = server.ReceiveFrameBytes();
                Assert.AreEqual(request, server.ReceiveFrameString());

                server.SendMore(serverId).Send(response);

                CollectionAssert.AreEqual(clientId, client.ReceiveFrameBytes());
                Assert.AreEqual(response, client.ReceiveFrameString());
            }
        }
    }
}
