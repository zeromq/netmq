using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ActorTests
    {
        [Test]
        public void Simple()
        {
            ShimAction shimAction = shim =>
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
                        Assert.AreEqual(2, msg.FrameCount);
                        Assert.AreEqual("Hello", msg[1].ConvertToString());
                        shim.SendFrame("World");
                    }
                }
            };

            using (var actor = NetMQActor.Create(shimAction))
            {
                actor.SendMoreFrame("Hello").SendFrame("Hello");

                Assert.AreEqual("World", actor.ReceiveFrameString());
            }
        }
    }
}
