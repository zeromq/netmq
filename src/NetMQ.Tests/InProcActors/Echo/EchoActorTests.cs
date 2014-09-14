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
            Actor<string> actor = new Actor<string>(NetMQContext.Create(), echoShimHandler, "Hello World");
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
            Actor<string> actor = new Actor<string>(NetMQContext.Create(), echoShimHandler, "Hello World");
            actor.SendMore(command);
            actor.Send(actorMessage);
            var result = actor.ReceiveString();
            string expectedEchoHandlerResult = "Error: invalid message to actor";
            Assert.AreEqual(expectedEchoHandlerResult, result);
            actor.Dispose();
        }

        [TestCase("")]
        [TestCase("12131")]
        [TestCase("Hello")]
        public void BadStatePassedToActor(string stateForActor)
        {
            string actorMessage = "whatever";
            EchoShimHandler echoShimHandler = new EchoShimHandler();
            
            try
            {
                //this will throw in this testcase, asw are supplying bad state for this EchoHandler
                Actor<string> actor = new Actor<string>(NetMQContext.Create(), echoShimHandler, stateForActor);
            }
            catch (Exception e)
            {
                Assert.AreEqual("Args were not correct, expected 'Hello World'", e.Message);
            }
        }
 
    }
}
