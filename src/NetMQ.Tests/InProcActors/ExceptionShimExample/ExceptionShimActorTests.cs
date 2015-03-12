using NUnit.Framework;

namespace NetMQ.Tests.InProcActors.ExceptionShimExample
{
    [TestFixture]
    public class ExceptionShimActorTests
    {
        [Test]
        public void ShimExceptionTest()
        {
            using (var context = NetMQContext.Create())
            using (var actor = NetMQActor.Create(context, new ExceptionShimHandler()))
            {
                actor.SendMore("SOME_COMMAND");
                actor.Send("Whatever");

                Assert.AreEqual(
                    "Error: Exception occurred Actors Shim threw an Exception",
                    actor.ReceiveFrameString());
            }
        }
    }
}
