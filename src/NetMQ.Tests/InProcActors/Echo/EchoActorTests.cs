using Xunit;

namespace NetMQ.Tests.InProcActors.Echo
{
    public class EchoActorTests
    {
        [Theory]
        [InlineData("I like NetMQ")]
        [InlineData("NetMQ Is quite awesome")]
        [InlineData("Agreed sockets on steroids with isotopes")]
        public void EchoActorSendReceiveTests(string actorMessage)
        {
            using (var actor = NetMQActor.Create(new EchoShimHandler()))
            {
                actor.SendMoreFrame("ECHO");
                actor.SendFrame(actorMessage);

                Assert.Equal(
                    $"ECHO BACK : {actorMessage}",
                    actor.ReceiveFrameString());
            }
        }

        [Theory]
        [InlineData("BadCommand1")]
        public void BadCommandTests(string command)
        {
            const string actorMessage = "whatever";

            using (var actor = NetMQActor.Create(new EchoShimHandler()))
            {
                actor.SendMoreFrame(command);
                actor.SendFrame(actorMessage);

                Assert.Equal("Error: invalid message to actor", actor.ReceiveFrameString());
            }
        }
    }
}
