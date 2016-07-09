using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class CleanupTests
    {
        [Test]
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
            NetMQConfig.Cleanup();
            stopwatch.Stop();

            Assert.Greater(stopwatch.ElapsedMilliseconds, 500);
        }

        [Test]
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
            NetMQConfig.Cleanup(false);
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 500);
        }

        [Test]
        public void NoBlockNoDispose()
        {
            var client = new DealerSocket(">tcp://localhost:5557");
            NetMQConfig.Cleanup(false);
        }
    }
}
