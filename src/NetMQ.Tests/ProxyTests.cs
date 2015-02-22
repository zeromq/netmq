using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ProxyTests
    {
        [Test]
        public void SendAndReceive()
        {
            using (var ctx = NetMQContext.Create())
            using (var front = ctx.CreateRouterSocket())
            using (var back = ctx.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                using (var client = ctx.CreateRequestSocket())
                using (var server = ctx.CreateResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.Send("hello");
                    Assert.AreEqual("hello", server.ReceiveString());
                    server.Send("reply");
                    Assert.AreEqual("reply", client.ReceiveString());
                }
            }
        }

        [Test]
        public void StartAndStopStateValidation()
        {
            using (var ctx = NetMQContext.Create())
            using (var front = ctx.CreateRouterSocket())
            using (var back = ctx.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = ctx.CreateRequestSocket())
                using (var server = ctx.CreateResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.Send("hello");
                    Assert.AreEqual("hello", server.ReceiveString());
                    server.Send("reply");
                    Assert.AreEqual("reply", client.ReceiveString());
                }

                Assert.Throws<InvalidOperationException>(proxy.Start);
                Assert.Throws<InvalidOperationException>(proxy.Start);
                Assert.Throws<InvalidOperationException>(proxy.Start);

                proxy.Stop(); // blocks until stopped

                Assert.Throws<InvalidOperationException>(proxy.Stop);
            }
        }
    }
}
