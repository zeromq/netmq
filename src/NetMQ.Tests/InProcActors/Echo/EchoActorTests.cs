using NUnit.Framework;

namespace NetMQ.Tests.InProcActors.Echo
{
    [TestFixture]
    public class EchoActorTests
    {
        [TestCase("I like NetMQ")]
        [TestCase("NetMQ Is quite awesome")]
        [TestCase("Agreed sockets on steroids with isotopes")]
        public void EchoActorSendReceiveTests(string actorMessage)
        {            
            using (var actor = NetMQActor.Create(new EchoShimHandler()))
            {
                actor.SendMoreFrame("ECHO");
                actor.SendFrame(actorMessage);

                Assert.AreEqual(
                    $"ECHO BACK : {actorMessage}",
                    actor.ReceiveFrameString());
            }
        }

        [TestCase("BadCommand1")]
        public void BadCommandTests(string command)
        {
            const string actorMessage = "whatever";
            
            using (var actor = NetMQActor.Create(new EchoShimHandler()))
            {
                actor.SendMoreFrame(command);
                actor.SendFrame(actorMessage);

                Assert.AreEqual("Error: invalid message to actor", actor.ReceiveFrameString());
            }
        }
    }
}
