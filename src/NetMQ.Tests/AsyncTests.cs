using System;
using System.Threading;
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
        
        [Fact]
        public void ReceiveMultipartMessage()
        {
            async Task ReceiveAsync()
            {
                using (var server = new RouterSocket("inproc://async"))
                using (var client = new DealerSocket("inproc://async"))
                {
                    var req = new NetMQMessage();
                    req.Append("Hello");
                    req.Append(new byte[]{ 0x00, 0x01, 0x02, 0x03 });

                    client.SendMultipartMessage(req);

                    NetMQMessage received = await server.ReceiveMultipartMessageAsync();
                    Assert.Equal("Hello", received[1].ConvertToString());
                    Assert.Equal(new byte[]{ 0x00, 0x01, 0x02, 0x03 }, received[2].Buffer);
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

        [Fact]
        public void SupportCancellation()
        {
            async Task ReceiveAsync()
            {
                using (var server = new RouterSocket("inproc://async"))
                {
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(1000);
                    await Assert.ThrowsAsync<TaskCanceledException>(
                        async () => await server.ReceiveMultipartMessageAsync(cancellationToken: cts.Token)
                    );
                    await Assert.ThrowsAsync<TaskCanceledException>(
                        async () => await server.ReceiveFrameBytesAsync(cts.Token)
                    );
                    await Assert.ThrowsAsync<TaskCanceledException>(
                        async () => await server.ReceiveFrameStringAsync(cts.Token)
                    );
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