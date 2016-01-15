using System;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ProxyTests
    {
        [Test]
        public void SendAndReceive()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
                }

                proxy.Stop();
            }
        }

        [Test]
        public void ControlSocketObservedMessages()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            using (var controlPush = new PushSocket())
            using (var controlPull = new PullSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                controlPush.Bind("inproc://control");
                controlPull.Connect("inproc://control");

                var proxy = new Proxy(front, back, controlPush);
                Task.Factory.StartNew(proxy.Start);

                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
                }

                Assert.IsNotNull(controlPull.ReceiveFrameBytes());     // receive identity
                Assert.IsEmpty(controlPull.ReceiveFrameString()); // pull terminator
                Assert.AreEqual("hello", controlPull.ReceiveFrameString());

                Assert.IsNotNull(controlPull.ReceiveFrameBytes());     // receive identity
                Assert.IsEmpty(controlPull.ReceiveFrameString()); // pull terminator
                Assert.AreEqual("reply", controlPull.ReceiveFrameString());

                proxy.Stop();
            }
        }

        [Test]
        public void SeparateControlSocketsObservedMessages()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            using (var controlInPush = new PushSocket())
            using (var controlInPull = new PullSocket())
            using (var controlOutPush = new PushSocket())
            using (var controlOutPull = new PullSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                controlInPush.Bind("inproc://controlIn");
                controlInPull.Connect("inproc://controlIn");
                controlOutPush.Bind("inproc://controlOut");
                controlOutPull.Connect("inproc://controlOut");

                var proxy = new Proxy(front, back, controlInPush, controlOutPush);
                Task.Factory.StartNew(proxy.Start);

                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
                }

                Assert.IsNotNull(controlInPull.ReceiveFrameBytes());     // receive identity
                Assert.IsEmpty(controlInPull.ReceiveFrameString()); // pull terminator
                Assert.AreEqual("hello", controlInPull.ReceiveFrameString());

                Assert.IsNotNull(controlOutPull.ReceiveFrameBytes());     // receive identity
                Assert.IsEmpty(controlOutPull.ReceiveFrameString()); // pull terminator
                Assert.AreEqual("reply", controlOutPull.ReceiveFrameString());

                proxy.Stop();
            }
        }

        [Test]
        public void StartAndStopStateValidation()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
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
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
                }

                proxy.Stop(); // blocks until stopped

                // Start it again
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());
                }

                proxy.Stop(); // blocks until stopped
            }
        }

        [Test]
        public void StoppingProxyDisengagesFunctionality()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back);
                Task.Factory.StartNew(proxy.Start);

                // Send a message through to ensure the proxy has started
                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");
                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());

                    proxy.Stop(); // blocks until stopped

                    using (var poller = new NetMQPoller { front, back })
                    {
                        poller.RunAsync();

                        client.SendFrame("anyone there?");

                        // Should no longer receive any messages
                        Assert.IsFalse(server.TrySkipFrame(TimeSpan.FromMilliseconds(50)));
                    }
                }
            }
        }

        [Test]
        public void TestProxySendAndReceiveWithExternalPoller()
        {
            using (var front = new RouterSocket())
            using (var back = new DealerSocket())
            using (var poller = new NetMQPoller { front, back })
            {
                front.Bind("inproc://frontend");
                back.Bind("inproc://backend");

                var proxy = new Proxy(front, back, null, poller);
                proxy.Start();

                poller.RunAsync();
                
                using (var client = new RequestSocket())
                using (var server = new ResponseSocket())
                {
                    client.Connect("inproc://frontend");
                    server.Connect("inproc://backend");

                    client.SendFrame("hello");
                    Assert.AreEqual("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.AreEqual("reply", client.ReceiveFrameString());

                    // Now stop the external poller
                    poller.Stop();

                    client.SendFrame("anyone there?");

                    // Should no longer receive any messages
                    Assert.IsFalse(server.TrySkipFrame(TimeSpan.FromMilliseconds(50)));
                }
            }
        }
    }
}
