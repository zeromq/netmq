using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ActorTests
    {        
        [Test]
        public void Simple()
        {
            using (var context = NetMQContext.Create())
            {
                ShimAction shimAction = shim =>
                {
                    shim.SignalOK();

                    while (true)
                    {
                        NetMQMessage msg = shim.ReceiveMessage();

                        string command = msg[0].ConvertToString();

                        if (command == NetMQActor.EndShimMessage)
                            break;

                        if (command == "Hello")
                            shim.Send("World");
                    }
                };

                using (var actor = NetMQActor.Create(context, shimAction))
                {
                    actor.SendMore("Hello");
                    actor.Send("Hello");
                    var result = actor.ReceiveString();
                    Assert.AreEqual("World", result);
                }
            }
        }
    }
}
