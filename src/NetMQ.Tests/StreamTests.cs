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

                byte[] id = client.Options.Identity;

                client.SendMore(id).Send("GET /\r\n");

                id = server.Receive();

                string message = server.ReceiveString();

                Assert.AreEqual(message, "GET /\r\n");

                const string response = "HTTP/1.0 200 OK\r\n" +
                                        "Content-Type: text/plain\r\n" +
                                        "\r\n" +
                                        "Hello, World!";

                server.SendMore(id).Send(response);

                client.Receive();
                message = client.ReceiveString();

                Assert.AreEqual(message, response);
            }
        }
    }
}
