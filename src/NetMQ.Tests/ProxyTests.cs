using System;
using System.Threading.Tasks;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ProxyTests : IClassFixture<CleanupAfterFixture>
    {
        /// <summary>Clean up before each test.</summary>
        public ProxyTests() => NetMQConfig.Cleanup();

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
                }

                proxy.Stop();
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
                }

                Assert.NotNull(controlPull.ReceiveFrameBytes()); // receive identity
                Assert.Empty(controlPull.ReceiveFrameString());  // pull terminator
                Assert.Equal("hello", controlPull.ReceiveFrameString());

                Assert.NotNull(controlPull.ReceiveFrameBytes()); // receive identity
                Assert.Empty(controlPull.ReceiveFrameString());  // pull terminator
                Assert.Equal("reply", controlPull.ReceiveFrameString());

                proxy.Stop();
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
                }

                Assert.NotNull(controlInPull.ReceiveFrameBytes()); // receive identity
                Assert.Empty(controlInPull.ReceiveFrameString());  // pull terminator
                Assert.Equal("hello", controlInPull.ReceiveFrameString());

                Assert.NotNull(controlOutPull.ReceiveFrameBytes()); // receive identity
                Assert.Empty(controlOutPull.ReceiveFrameString());  // pull terminator
                Assert.Equal("reply", controlOutPull.ReceiveFrameString());

                proxy.Stop();
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
                }

                Assert.Throws<InvalidOperationException>((Action)proxy.Start);
                Assert.Throws<InvalidOperationException>((Action)proxy.Start);
                Assert.Throws<InvalidOperationException>((Action)proxy.Start);

                proxy.Stop(); // blocks until stopped

                Assert.Throws<InvalidOperationException>((Action)proxy.Stop);
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());
                }

                proxy.Stop(); // blocks until stopped
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());

                    proxy.Stop(); // blocks until stopped

                    using (var poller = new NetMQPoller { front, back })
                    {
                        poller.RunAsync();

                        client.SendFrame("anyone there?");

                        // Should no longer receive any messages
                        Assert.False(server.TrySkipFrame(TimeSpan.FromMilliseconds(50)));
                    }
                }
            }
        }

        [Fact]
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
                    Assert.Equal("hello", server.ReceiveFrameString());
                    server.SendFrame("reply");
                    Assert.Equal("reply", client.ReceiveFrameString());

                    // Now stop the external poller
                    Assert.True(poller.IsRunning);
                    poller.Stop();
                    Assert.False(poller.IsRunning);

                    client.SendFrame("anyone there?");

                    // Should no longer receive any messages
                    Assert.False(server.TrySkipFrame(TimeSpan.FromMilliseconds(50)));
                }
            }
        }
    }
}
