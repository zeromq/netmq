using System;
using System.Diagnostics;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class CleanupTests
    {
        [Fact]
        public void Block()
        {
            const int count = 1000;

            NetMQConfig.Linger = TimeSpan.FromSeconds(0.5);

            using (var client = new DealerSocket(">tcp://localhost:5557"))
            {
                // Sending a lot of messages
                client.Options.SendHighWatermark = count;
                for (int i = 0; i < count; i++)
                {
                    client.SendFrame("Hello");
                }
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            NetMQConfig.Cleanup(block: true);
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds > 500);
        }

        [Fact]
        public void NoBlock()
        {
            const int count = 1000;

            NetMQConfig.Linger = TimeSpan.FromSeconds(0.5);

            using (var client = new DealerSocket(">tcp://localhost:5557"))
            {
                // Sending a lot of messages
                client.Options.SendHighWatermark = count;
                for (int i = 0; i < count; i++)
                {
                    client.SendFrame("Hello");
                }
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            NetMQConfig.Cleanup(block: false);
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 500);
        }

        [Fact]
        public void NoBlockNoDispose()
        {
            var client = new DealerSocket(">tcp://localhost:5557");
            NetMQConfig.Cleanup(block: false);
        }
    }
}
