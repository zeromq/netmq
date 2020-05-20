using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ClientServer
    {
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

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await server.ReceiveStringAsync(source.Token));
        }
    }
}