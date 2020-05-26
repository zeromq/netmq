using System.Threading;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ScatterGather
    {
        [Fact]
        public void TestTcp()
        {
            using var scatter = new ScatterSocket();
            using var gather = new GatherSocket();

            int port = scatter.BindRandomPort("tcp://*");
            gather.Connect($"tcp://127.0.0.1:{port}");
            
            scatter.Send("1");
            scatter.Send("2");

            var m1 = gather.ReceiveString();
            Assert.Equal("1", m1);
            
            var m2 = gather.ReceiveString();
            Assert.Equal("2", m2);
        }
        
        [Fact]
        public void TestBlocking()
        {
            using var scatter = new ScatterSocket();
            using var gather = new GatherSocket();
            using var gather2 = new GatherSocket();

            scatter.Bind("inproc://test-scatter-gather");
            gather.Connect("inproc://test-scatter-gather");
            gather2.Connect("inproc://test-scatter-gather");
            
            scatter.Send("1");
            scatter.Send("2");

            var m1 = gather.ReceiveString();
            Assert.Equal("1", m1);
            
            var m2 = gather2.ReceiveString();
            Assert.Equal("2", m2);
        }
        
        [Fact]
        public async void TestAsync()
        {
            using var scatter = new ScatterSocket();
            using var gather = new GatherSocket();
            using var gather2 = new GatherSocket();

            scatter.Bind("inproc://test-scatter-gather");
            gather.Connect("inproc://test-scatter-gather");
            gather2.Connect("inproc://test-scatter-gather");
            
            await scatter.SendAsync("1");
            await scatter.SendAsync("2");

            var m1 = await gather.ReceiveStringAsync();
            Assert.Equal("1", m1);
            
            var m2 = await gather2.ReceiveStringAsync();
            Assert.Equal("2", m2);
        }
    }
}