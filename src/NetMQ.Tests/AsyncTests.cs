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

                    Assert.Equal("Hello", message);

                    server.SendMoreFrame(routingKey);
                    server.SendFrame(new[] { (byte) 0 });

                    var (bytes, _) = await client.ReceiveFrameBytesAsync();

                    Assert.Equal(new[] { (byte) 0 }, bytes);
                }
            }

            using (var runtime = new NetMQRuntime())
            {
                var t = ReceiveAsync();
                runtime.Run(t);

                if (t.IsFaulted && t.Exception is AggregateException exc)
                {
                    throw exc.GetBaseException();
                }
            }
        }        
            }
        }
    }
}