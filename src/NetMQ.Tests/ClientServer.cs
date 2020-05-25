using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace NetMQ.Tests
{
    public class ClientServer
    {
        private readonly ITestOutputHelper m_testOutputHelper;

        public ClientServer(ITestOutputHelper testOutputHelper)
        {
            m_testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Inproc()
        {
            using var server = new ServerSocket();
            using var client = new ClientSocket();
            server.Bind("inproc://client-server");
            client.Connect("inproc://client-server");
            
            client.Send("Hello");
            var (routingId, clientMsg) = server.ReceiveString();
            Assert.NotEqual<uint>(0, routingId);
            Assert.Equal("Hello", clientMsg);
            
            server.Send(routingId, "World");
            var serverMsg = client.ReceiveString();
            Assert.Equal("World", serverMsg);
        }
        
        [Fact]
        public void Tcp()
        {
            using var server = new ServerSocket();
            using var client = new ClientSocket();
            int port = server.BindRandomPort("tcp://*");
            client.Connect($"tcp://localhost:{port}");
            
            client.Send("Hello");
            var (routingId, clientMsg) = server.ReceiveString();
            Assert.NotEqual<uint>(0, routingId);
            Assert.Equal("Hello", clientMsg);
            
            server.Send(routingId, "World");
            var serverMsg = client.ReceiveString();
            Assert.Equal("World", serverMsg);
        }

        [Fact]
        public async void Async()
        {
            using var server = new ServerSocket();
            using var client = new ClientSocket();
            int port = server.BindRandomPort("tcp://*");
            client.Connect($"tcp://localhost:{port}");
            
            await client.SendAsync("Hello");
            var (routingId, clientMsg) = await server.ReceiveStringAsync();
            Assert.NotEqual<uint>(0, routingId);
            Assert.Equal("Hello", clientMsg);
            
            await server.SendAsync(routingId, "World");
            var serverMsg = await client.ReceiveStringAsync();
            Assert.Equal("World", serverMsg);
        }

        [Fact]
        public async void AsyncWithCancellationToken()
        {
            using CancellationTokenSource source = new CancellationTokenSource();
            using var server = new ServerSocket();
            
            source.CancelAfter(100);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await server.ReceiveStringAsync(source.Token));
        }

#if NETCOREAPP3_1

        [Fact(Timeout = 120)]
        public async void AsyncEnumerableCanceled()
        {
            using CancellationTokenSource source = new CancellationTokenSource();
            using var server = new ServerSocket();
            
            source.CancelAfter(100);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var msg in server.ReceiveStringAsyncEnumerable(source.Token))
                    return;
            });
        }
        
        [Fact(Timeout = 1000)]
        public void AsyncEnumerable()
        {
            using var server = new ServerSocket();
            int port = server.BindRandomPort("tcp://*");
            
            using var client = new ClientSocket();
            client.Connect($"tcp://127.0.0.1:{port}");

            int totalCount = 0;

            var t1 = Task.Run(async () =>
            {
                int count = 0;
                
                await foreach (var (_, msg) in server.ReceiveStringAsyncEnumerable())
                {
                    count++;
                    Interlocked.Increment(ref totalCount);

                    if (msg == "1")
                    {
                        m_testOutputHelper.WriteLine($"T1 read {count} messages");
                        return;
                    }
                }
            });
            
            var t2 = Task.Run(async () =>  {
                int count = 0;
                
                await foreach (var (_, msg) in server.ReceiveStringAsyncEnumerable())
                {
                    count++;
                    Interlocked.Increment(ref totalCount);

                    if (msg == "1")
                    {
                        m_testOutputHelper.WriteLine($"T2 read {count} messages");
                        return;
                    }
                }
            });

            for (int i = 0; i < 15000; i++)
            {
                client.Send("0");
            }
            
            // Send the end message to both of the threads
            client.Send("1");
            client.Send("1");

            t1.Wait();
            t2.Wait();
            
            Assert.Equal(15002, totalCount);
        }

#endif
    }
}