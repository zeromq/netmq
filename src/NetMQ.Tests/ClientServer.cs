using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace NetMQ.Tests
{
    public class ClientServer : IClassFixture<CleanupAfterFixture>
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

        // These tests block on purpose, always with a timeout, to detect a hang.
#pragma warning disable xUnit1031
        [Fact]
        public void DisposeWhileReceiving()
        {
            using var server = new ServerSocket();
            using (var client = new ClientSocket())
            {
                int port = server.BindRandomPort("tcp://127.0.0.1");
                client.Connect($"tcp://127.0.0.1:{port}");

                client.Send("Hello");
                server.ReceiveString();

                var receive = Task.Run(() => server.ReceiveString());
                Thread.Sleep(100);
                server.Dispose();

                var ex = Assert.Throws<AggregateException>(() => receive.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsType<ObjectDisposedException>(ex.InnerException);
            }

            Assert.True(Task.Run(() => NetMQConfig.Cleanup()).Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void DisposeWhileReceivingWithoutPeers()
        {
            using var server = new ServerSocket();

            var receive = Task.Run(() => server.ReceiveString());
            Thread.Sleep(100);
            server.Dispose();

            var ex = Assert.Throws<AggregateException>(() => receive.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsType<ObjectDisposedException>(ex.InnerException);

            Assert.True(Task.Run(() => NetMQConfig.Cleanup()).Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void DisposeWhileConnecting()
        {
            using var server = new ServerSocket();
            int port = server.BindRandomPort("tcp://127.0.0.1");
            string address = $"tcp://127.0.0.1:{port}";

            for (int i = 0; i < 20; i++)
            {
                using var client = new ClientSocket();

                var connect = Task.Run(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        client.Connect(address);
                        client.Disconnect(address);
                    }
                });
                Thread.Sleep(10);
                client.Dispose();

                var ex = Assert.Throws<AggregateException>(() => connect.Wait(TimeSpan.FromSeconds(10)));
                Assert.IsType<ObjectDisposedException>(ex.InnerException);
            }

            server.Dispose();
            Assert.True(Task.Run(() => NetMQConfig.Cleanup()).Wait(TimeSpan.FromSeconds(5)));
        }
#pragma warning restore xUnit1031

        [Fact]
        public async Task Async()
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
        public async Task AsyncWithCancellationToken()
        {
            using CancellationTokenSource source = new CancellationTokenSource();
            using var server = new ServerSocket();
            
            source.CancelAfter(100);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await server.ReceiveStringAsync(source.Token));
        }

        [Fact(Timeout = 5000)]
        public async Task AsyncEnumerableCanceled()
        {
            using CancellationTokenSource source = new CancellationTokenSource();
            using var server = new ServerSocket();
            
            source.CancelAfter(500);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var msg in server.ReceiveStringAsyncEnumerable(source.Token))
                    return;
            });
        }
        
        [Fact(Timeout = 1000)]
        public async Task AsyncEnumerable()
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

            await Task.WhenAll(t1, t2);
            
            Assert.Equal(15002, totalCount);
        }

        /// <summary>
        /// Sends from both ends of an inproc pair at once while each end keeps draining, so both
        /// flushes find a passive peer reader and notify the peer's mailbox. Before the fix each
        /// sender held its own socket's lock while taking the peer's, and the two sends deadlocked.
        /// </summary>
        [Fact]
        public void InprocConcurrentSendsInBothDirectionsDoNotDeadlock()
        {
            using var server = new ServerSocket();
            using var client = new ClientSocket();
            server.Bind("inproc://client-server-concurrent");
            client.Connect("inproc://client-server-concurrent");

            client.Send("Hello");
            var (routingId, _) = server.ReceiveString();

            const int count = 20000;
            using var start = new Barrier(2);

            var clientThread = new Thread(() =>
            {
                start.SignalAndWait();
                int received = 0;
                for (int i = 0; i < count; i++)
                {
                    client.Send("c");
                    while (client.TryReceiveString(TimeSpan.Zero, out _))
                        received++;
                }

                // Keep draining so the server's last sends are not held at the high-water mark.
                for (; received < count; received++)
                    client.ReceiveString();
            }) { IsBackground = true };
            var serverThread = new Thread(() =>
            {
                start.SignalAndWait();
                int received = 0;
                for (int i = 0; i < count; i++)
                {
                    server.Send(routingId, "s");
                    while (server.TryReceiveString(out _, out _))
                        received++;
                }

                for (; received < count; received++)
                    server.ReceiveString();
            }) { IsBackground = true };

            clientThread.Start();
            serverThread.Start();

            Assert.True(clientThread.Join(TimeSpan.FromSeconds(30)), "client send deadlocked");
            Assert.True(serverThread.Join(TimeSpan.FromSeconds(30)), "server send deadlocked");
        }

    }
}