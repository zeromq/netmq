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
            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            {
                router.Options.RouterMandatory = true;
                router.BindRandomPort("tcp://*");

                Assert.Throws<HostUnreachableException>(() => router.SendMore("UNKNOWN").Send("Hello"));
            }
        }

        [Test]
        public void ReceiveReadyDot35Bug()
        {
            // In .NET 3.5, we saw an issue where ReceiveReady would be raised every second despite nothing being received

            using (var context = NetMQContext.Create())
            using (var server = context.CreateRouterSocket())
            {
                server.BindRandomPort("tcp://127.0.0.1");
                server.ReceiveReady += (s, e) =>
                {
                    Assert.Fail("Should not receive");
                };

                Assert.IsFalse(server.Poll(TimeSpan.FromMilliseconds(1500)));
            }
        }
    }
}
