using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Actors;
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
            EchoShimHandler echoShimHandler = new EchoShimHandler();
            Actor actor = new Actor(NetMQContext.Create(), null,echoShimHandler, new object[] { "Hello World" });
            actor.SendMore("ECHO");
            actor.Send(actorMessage);
            var result = actor.ReceiveString();
            string expectedEchoHandlerResult = string.Format("ECHO BACK : {0}", actorMessage);
            Assert.AreEqual(expectedEchoHandlerResult, result);
            actor.Dispose();            
        }

        [TestCase("BadCommand1")]
        public void BadCommandTests(string command)
        {
            string actorMessage = "whatever";
            EchoShimHandler echoShimHandler = new EchoShimHandler();
            Action<Exception> pipeExceptionHandler = (ex) =>
            {
                Assert.AreEqual("Unexpected command",ex.Message);
            };
            Actor actor = new Actor(NetMQContext.Create(), pipeExceptionHandler, echoShimHandler, new object[] { "Hello World" });
            actor.SendMore(command);
            actor.Send(actorMessage);
            var result = actor.ReceiveString();
            string expectedEchoHandlerResult = string.Format("ECHO BACK : {0}", actorMessage);
            Assert.AreEqual(expectedEchoHandlerResult, result);
            actor.Dispose();
        }
 
    }
}
