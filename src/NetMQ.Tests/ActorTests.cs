using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ActorTests : IClassFixture<CleanupAfterFixture>
    {
        public ActorTests() => NetMQConfig.Cleanup();

        [Fact]
        public void Simple()
        {
            void ShimAction(PairSocket shim)
            {
                shim.SignalOK();

                while (true)
                {
                    var msg = shim.ReceiveMultipartMessage();
                    var command = msg[0].ConvertToString();

                    if (command == NetMQActor.EndShimMessage)
                        break;

                    if (command == "Hello")
                    {
                        Assert.Equal(2, msg.FrameCount);
                        Assert.Equal("Hello", msg[1].ConvertToString());
                        shim.SendFrame("World");
                    }
                }
            }

            using (var actor = NetMQActor.Create(ShimAction))
            {
                actor.SendMoreFrame("Hello").SendFrame("Hello");

                Assert.Equal("World", actor.ReceiveFrameString());
            }
        }
    }
}
