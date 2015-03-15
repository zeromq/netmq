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
            using (var context = NetMQContext.Create())
            using (var actor = NetMQActor.Create(context, new EchoShimHandler()))
            {
                actor.SendMore("ECHO");
                actor.Send(actorMessage);

                Assert.AreEqual(
                    string.Format("ECHO BACK : {0}", actorMessage),
                    actor.ReceiveFrameString());
            }
        }

        [TestCase("BadCommand1")]
        public void BadCommandTests(string command)
        {
            const string actorMessage = "whatever";

            using (var context = NetMQContext.Create())
            using (var actor = NetMQActor.Create(context, new EchoShimHandler()))
            {
                actor.SendMore(command);
                actor.Send(actorMessage);

                Assert.AreEqual("Error: invalid message to actor", actor.ReceiveFrameString());
            }
        }
    }
}
