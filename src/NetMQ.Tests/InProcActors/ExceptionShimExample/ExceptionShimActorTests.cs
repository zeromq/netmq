using NetMQ.Actors;
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
            {
                ExceptionShimHandler exceptionShimHandler = new ExceptionShimHandler();
                using (Actor<object> actor = new Actor<object>(context, exceptionShimHandler, null))
                {
                    actor.SendMore("SOME_COMMAND");
                    actor.Send("Whatever");
                    var result = actor.ReceiveString();
                    string expectedHandlerResult = "Error: Exception occurred Actors Shim threw an Exception";
                    Assert.AreEqual(expectedHandlerResult, result);
                }
            }
        }
    }
}
