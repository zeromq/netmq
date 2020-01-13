using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using Xunit;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable ExceptionNotDocumented

namespace NetMQ.Tests
{
    public class ZMTPTests : IClassFixture<CleanupAfterFixture>
    {
        public ZMTPTests() => NetMQConfig.Cleanup();

        [Fact]
        public void V2Test()
        {
            using (var raw = new StreamSocket())
            using (var socket = new DealerSocket())
            {
                int port = raw.BindRandomPort("tcp://*");
                socket.Connect($"tcp://localhost:{port}");

                var routingId = raw.ReceiveFrameBytes();
                var preamble = raw.ReceiveFrameBytes();
                Assert.Equal(10, preamble.Length);

                raw.SendMoreFrame(routingId).SendFrame(new byte[] {0xff,0,0,0,0,0,0,0,0,0x7f,1,5}); // Signature
                raw.SendMoreFrame(routingId).SendFrame(new byte[] {0, 0}); // Empty Identity
                raw.SendMoreFrame(routingId).SendFrame(new byte[] {0, 1, 5}); // One byte message
                
                // Receive rest of the greeting
                raw.SkipFrame(); // RoutingId
                var signature = raw.ReceiveFrameBytes();
                
                if (signature.Length == 2)
                    Assert.Equal(new byte[] {1, 5}, signature);
                else if (signature.Length == 1)
                {
                    // Receive rest of the greeting
                    raw.SkipFrame(); // RoutingId
                    signature = raw.ReceiveFrameBytes();
                    Assert.Equal(new byte[] {5}, signature);
                }
                
                // Receive the identity
                raw.SkipFrame(); // RoutingId
                var identity = raw.ReceiveFrameBytes();
                Assert.Equal(new byte[] {0, 0}, identity);
                
                // Receiving msg send by the raw
                var msg = socket.ReceiveFrameBytes();
                Assert.Equal(new byte[] {5}, msg);
                
                // Sending msg from socket to raw
                socket.SendFrame(new byte[]{6});
                raw.SkipFrame(); // RoutingId
                msg = raw.ReceiveFrameBytes();
                Assert.Equal(new byte[]{0,1,6}, msg);
            }
        }
    }
}