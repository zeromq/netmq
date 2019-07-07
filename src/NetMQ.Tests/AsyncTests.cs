using System;
using System.Threading.Tasks;
using NetMQ.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace NetMQ.Tests
{
    public class AsyncTests
    {
        private readonly ITestOutputHelper m_testOutputHelper;

        public AsyncTests(ITestOutputHelper testOutputHelper)
        {
            m_testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Receive()
        {
            async Task ReceiveAsync()
            {
                using (var server = new RouterSocket("inproc://async"))
                using (var client = new DealerSocket("inproc://async"))
                {
                    client.SendFrame("Hello");

                    var (routingKey, _) = await server.ReceiveRoutingKeyAsync();
                    var (message, _) = await server.ReceiveFrameStringAsync();

                    Assert.Equal(message, "Hello");

                    server.SendMoreFrame(routingKey);
                    server.SendFrame(new[] { (byte) 0 });

                    var (bytes, _) = await client.ReceiveFrameBytesAsync();

                    Assert.Equal(bytes[0], 0);
                }
            }

            using (var runtime = new NetMQRuntime())
            {
                runtime.Run(ReceiveAsync());
            }
        }
    }
}