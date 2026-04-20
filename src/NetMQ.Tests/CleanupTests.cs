using System;
using System.Diagnostics;
using System.Threading;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class CleanupTests : IClassFixture<CleanupAfterFixture>
    {
        public CleanupTests() => NetMQConfig.Cleanup();

        [Fact(Skip = "Failing occasionally")]
        public void Block()
        {
            const int count = 1000;

            NetMQConfig.Linger = TimeSpan.FromSeconds(0.5);

            using (var client = new DealerSocket(">tcp://localhost:5557"))
            {
                // Sending a lot of messages
                client.Options.SendHighWatermark = count;

                for (int i = 0; i < count; i++)
                    client.SendFrame("Hello");
            }

            var stopwatch = Stopwatch.StartNew();

            NetMQConfig.Cleanup(block: true);

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
                    client.SendFrame("Hello");
            }

            var stopwatch = Stopwatch.StartNew();

            NetMQConfig.Cleanup(block: false);

            Assert.True(stopwatch.ElapsedMilliseconds < 500);
        }

        [Fact]
        public void NoBlockNoDispose()
        {
            new DealerSocket(">tcp://localhost:5557");

            NetMQConfig.Cleanup(block: false);
        }

        [Fact]
        public void NoBlockCompletesInBoundedTime()
        {
            // Regression test for https://github.com/zeromq/netmq/issues/1040
            // Cleanup(block: false) must return quickly even when a socket has not been
            // disposed and the internal poller is actively calling Socket.Select.
            // On macOS, Socket.Select can block indefinitely when passed both a read list
            // and an error list, causing Cleanup to hang forever without this guard.
            _ = new DealerSocket(">tcp://localhost:5557"); // intentionally not disposed

            // Run cleanup on a background (daemon) thread so the process can still exit
            // if the thread gets stuck. IsBackground = true prevents it from blocking
            // process shutdown if a regression causes Cleanup to hang.
            var thread = new Thread(() => NetMQConfig.Cleanup(block: false)) { IsBackground = true };
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)),
                "Cleanup(block: false) did not complete within 10 seconds");
        }
    }
}
