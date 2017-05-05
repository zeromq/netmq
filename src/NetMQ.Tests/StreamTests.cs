using Xunit;

using NetMQ.Sockets;

namespace NetMQ.Tests
{
    public class StreamTests : IClassFixture<CleanupAfterFixture>
    {
        public StreamTests() => NetMQConfig.Cleanup();

        [Fact]
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
                Assert.Equal(request, server.ReceiveFrameString());

                server.SendMoreFrame(serverId).SendFrame(response);

                Assert.Equal(clientId, client.ReceiveFrameBytes());
                Assert.Equal(response, client.ReceiveFrameString());
            }
        }
    }
}
