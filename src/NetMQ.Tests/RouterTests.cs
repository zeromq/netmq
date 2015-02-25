using System;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class RouterTests
    {
        [Test]
        public void Mandatory()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var router = context.CreateRouterSocket())
                {
                    router.Options.RouterMandatory = true;
                    router.Bind("tcp://*:5555");

                    Assert.Throws<HostUnreachableException>(() => router.SendMore("UNKOWN").Send("Hello"));
                }
            }
        }

        [Test]
        public void ReceiveReadyDot35Bug()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var server = context.CreateRouterSocket())
                {                    
                    server.Bind("tcp://127.0.0.1:5555");
                    server.ReceiveReady += (sender, e) =>
                    {
                        //no data receive but every 1s to display ReceiveReady.
                        Console.WriteLine("ReceiveReady!");                        
                    };

                    Assert.IsFalse(server.Poll(TimeSpan.FromMilliseconds(1000)));                                        
                }
            }
        }
    }
}
