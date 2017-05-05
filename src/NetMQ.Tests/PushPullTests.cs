using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class PushPullTests : IClassFixture<CleanupAfterFixture>
    {
        public PushPullTests() => NetMQConfig.Cleanup();

        [Fact]
        public void SimplePushPull()
        {
            using (var pullSocket = new PullSocket())
            using (var pushSocket = new PushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.SendFrame("hello");

                Assert.Equal("hello", pullSocket.ReceiveFrameString());
            }
        }

        [Fact]
        public void EmptyMessage()
        {
            using (var pullSocket = new PullSocket())
            using (var pushSocket = new PushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.SendFrame(new byte[300]);

                Assert.Equal(300, pullSocket.ReceiveFrameString().Length);
            }
        }
    }
}
