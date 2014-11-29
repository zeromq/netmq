using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;
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
                using (NetMQActor actor = NetMQActor.Create(context, shim =>
                {
                    shim.SignalOK();

                    while (true)
                    {
                        NetMQMessage msg = shim.ReceiveMessage();

                        string command = msg[0].ConvertToString();

                        if (command == NetMQActor.DisposePipeMessage)
                            break;

                        else if (command == "Hello")
                        {
                            shim.Send("World");
                        }
                    }
                }))
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
