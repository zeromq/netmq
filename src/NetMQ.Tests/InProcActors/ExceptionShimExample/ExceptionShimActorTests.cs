using NUnit.Framework;

namespace NetMQ.Tests.InProcActors.ExceptionShimExample
{
    [TestFixture]
    public class ExceptionShimActorTests
    {
        [Test]
        public void ShimExceptionTest()
        {            
            using (var actor = NetMQActor.Create(new ExceptionShimHandler()))
            {
                actor.SendMoreFrame("SOME_COMMAND");
                actor.SendFrame("Whatever");

                Assert.AreEqual(
                    "Error: Exception occurred Actors Shim threw an Exception",
                    actor.ReceiveFrameString());
            }
        }
    }
}
