using System.Threading;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class NetMQProactorTests : IClassFixture<CleanupAfterFixture>
    {
        public NetMQProactorTests() => NetMQConfig.Cleanup();

        [Fact]
        public void ReceiveMessage()
        {
            using (var server = new DealerSocket("@tcp://127.0.0.1:5555"))
            using (var client = new DealerSocket(">tcp://127.0.0.1:5555"))
            using (var manualResetEvent = new ManualResetEvent(false))
            using (new NetMQProactor(client, (socket, message) =>
            {
                manualResetEvent.Set();
                Assert.Same(client, socket);
            }))
            {
                server.SendFrame("Hello");

                Assert.True(manualResetEvent.WaitOne(100));
            }
        }
    }
}
