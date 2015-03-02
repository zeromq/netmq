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
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
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
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            using (var controlPush = context.CreatePushSocket())
            using (var controlPull = context.CreatePullSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                controlPush.Bind("inproc://control");
                controlPull.Connect("inproc://control");

                var proxy = new Proxy(front, back, controlPush);
                Task.Factory.StartNew(proxy.Start);

                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
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
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
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
        public void StartAgainAfterStop()
        {
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.Send("hello");
                    Assert.AreEqual("hello", server.ReceiveString());
                    server.Send("reply");
                    Assert.AreEqual("reply", client.ReceiveString());
                }

                proxy.Stop(); // blocks until stopped

                // Start it again
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.Send("hello");
                    Assert.AreEqual("hello", server.ReceiveString());
                    server.Send("reply");
                    Assert.AreEqual("reply", client.ReceiveString());
                }

                proxy.Stop(); // blocks until stopped
            }
        }

        [Test]
        public void StoppingProxyDisengagesFunctionality()
        {
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
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

        [Test]
        public void TestProxySendAndReceiveWithExternalPoller()
        {
            using (var context = NetMQContext.Create())
            using (var front = context.CreateRouterSocket())
            using (var back = context.CreateDealerSocket())
            using (var poller = new Poller(front, back))
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back, null, poller);

                poller.PollTillCancelledNonBlocking();

                proxy.Start();

                using (var client = context.CreateRequestSocket())
                using (var server = context.CreateResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.Send("hello");
                    Assert.AreEqual("hello", server.ReceiveString());
                    server.Send("reply");
                    Assert.AreEqual("reply", client.ReceiveString());

                    // Now stop the external poller
                    poller.CancelAndJoin();

                    client.Send("anyone there?");

                    // Should no longer receive any messages
                    Assert.IsNull(server.ReceiveString(TimeSpan.FromMilliseconds(50)));
                }
            }
        }
    }
}
