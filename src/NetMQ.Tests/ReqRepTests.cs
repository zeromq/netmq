using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ReqRepTests : IClassFixture<CleanupAfterFixture>
    {
        public ReqRepTests() => NetMQConfig.Cleanup();

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SimpleReqRep(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);

                req.SendFrame("Hi");

                Assert.Equal(new[] { "Hi" }, rep.ReceiveMultipartStrings());

                rep.SendFrame("Hi2");

                Assert.Equal(new[] { "Hi2" }, req.ReceiveMultipartStrings());
            }
        }

        [Fact]
        public void SendingTwoRequestsInARow()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendFrame("Hi");

                rep.SkipFrame();

                Assert.Throws<FiniteStateMachineException>(() => req.SendFrame("Hi2"));
            }
        }

        [Fact]
        public void ReceiveBeforeSending()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Fact]
        public void SendMessageInResponseBeforeReceiving()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => rep.SendFrame("1"));
            }
        }

        [Fact]
        public void SendMultipartMessage()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendMoreFrame("Hello").SendFrame("World");

                Assert.Equal(new[] { "Hello", "World" }, rep.ReceiveMultipartStrings());

                rep.SendMoreFrame("Hello").SendFrame("Back");

                Assert.Equal(new[] { "Hello", "Back" }, req.ReceiveMultipartStrings());
            }
        }
    }
}
