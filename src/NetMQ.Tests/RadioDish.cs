using System;
using System.Threading;
using NetMQ.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace NetMQ.Tests
{
    public class RadioDish
    {
        [Fact]
        public void TestTcp()
        {
            using var radio = new RadioSocket();
            using var dish = new DishSocket();
            dish.Join("2");

            int port = radio.BindRandomPort("tcp://*");
            dish.Connect($"tcp://127.0.0.1:{port}");
            
            Thread.Sleep(100);

            radio.Send("1", "HELLO");
            radio.Send("2", "HELLO");

            var (group,msg) = dish.ReceiveString();
            Assert.Equal("2", group);
            Assert.Equal("HELLO", msg);
        }
        
        [Fact]
        public void TestBlocking()
        {
            using var radio = new RadioSocket();
            using var dish = new DishSocket();
            dish.Join("2");

            radio.Bind("inproc://test-radio-dish");
            dish.Connect("inproc://test-radio-dish");
            
            radio.Send("1", "HELLO");
            radio.Send("2", "HELLO");

            var (group,msg) = dish.ReceiveString();
            Assert.Equal("2", group);
            Assert.Equal("HELLO", msg);
        }
        
        [Fact]
        public async void TestAsync()
        {
            using var radio = new RadioSocket();
            using var dish = new DishSocket();
            dish.Join("2");

            int port = radio.BindRandomPort("tcp://*");
            dish.Connect($"tcp://127.0.0.1:{port}");
            
            Thread.Sleep(100);

            await radio.SendAsync("1", "HELLO");
            await radio.SendAsync("2", "HELLO");

            var (group,msg) = await dish.ReceiveStringAsync();
            Assert.Equal("2", group);
            Assert.Equal("HELLO", msg);
        }
    }
}