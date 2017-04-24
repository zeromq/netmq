using Xunit;

namespace NetMQ.Tests.InProcActors.ExceptionShimExample
{
    public class ExceptionShimActorTests
    {
        [Fact]
        public void ShimExceptionTest()
        {            
            using (var actor = NetMQActor.Create(new ExceptionShimHandler()))
            {
                actor.SendMoreFrame("SOME_COMMAND");
                actor.SendFrame("Whatever");

                Assert.Equal(
                    "Error: Exception occurred Actors Shim threw an Exception",
                    actor.ReceiveFrameString());
            }
        }
    }
}
