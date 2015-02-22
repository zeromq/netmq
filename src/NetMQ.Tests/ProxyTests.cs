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

                proxy.Stop();
            }
        }

        [Test]
        public void ControlSocketObservedMessages()
        {
            using (var ctx = NetMQContext.Create())
            using (var front = ctx.CreateRouterSocket())
            using (var back = ctx.CreateDealerSocket())
            using (var controlPush = ctx.CreatePushSocket())
            using (var controlPull = ctx.CreatePullSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                controlPush.Bind("inproc://control");
                controlPull.Connect("inproc://control");

                var proxy = new Proxy(front, back, controlPush);
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

                Assert.IsNotNull(controlPull.Receive());     // receive identity
                Assert.IsEmpty(controlPull.ReceiveString()); // pull terminator
                Assert.AreEqual("hello", controlPull.ReceiveString());

                Assert.IsNotNull(controlPull.Receive());     // receive identity
                Assert.IsEmpty(controlPull.ReceiveString()); // pull terminator
                Assert.AreEqual("reply", controlPull.ReceiveString());

                proxy.Stop();
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

        [Test]
        public void StoppingProxyDisengagesFunctionality()
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

                    proxy.Stop(); // blocks until stopped

                    using (var poller = new Poller(front, back))
                    {
                        poller.PollTillCancelledNonBlocking();

                        client.Send("anyone there?");

                        // Should no longer receive any messages
                        Assert.IsNull(server.ReceiveString(TimeSpan.FromMilliseconds(50)));

                        poller.CancelAndJoin();
                    }
                }
            }
        }
    }
}
