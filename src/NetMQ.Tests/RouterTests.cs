using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
    }
}
