using System;
using System.Net;
using System.Net.Sockets;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class TcpListenerTests : IClassFixture<CleanupAfterFixture>
    {
        public TcpListenerTests() => NetMQConfig.Cleanup();

        [Fact]
        public void AcceptedSocketFailingSetupDoesNotCrashProcessOrStopListener()
        {
            using (var pub = new PublisherSocket())
            {
                int port = pub.BindRandomPort("tcp://127.0.0.1");

                // Hammer the listener with connections that are reset immediately:
                // SO_LINGER=0 + Close sends RST, which can land between the proactor
                // completing an accept and TcpListener applying socket options to the
                // accepted socket. On macOS the option calls then throw
                // SocketException (EINVAL). Unhandled, that exception killed the
                // proactor thread and with it the process; swallowed carelessly, it
                // skips the re-arming Accept() and the listener goes deaf instead.
                for (int i = 0; i < 400; i++)
                {
                    using (var raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        raw.LingerState = new LingerOption(true, 0);
                        try
                        {
                            raw.Connect(IPAddress.Loopback, port);
                        }
                        catch (SocketException)
                        {
                        }
                    }
                }

                // The listener must still be alive and accepting: a subscriber that
                // connects after the storm must complete the handshake and receive.
                using (var sub = new SubscriberSocket())
                {
                    sub.Connect("tcp://127.0.0.1:" + port);
                    sub.SubscribeToAnyTopic();

                    var received = false;
                    for (int i = 0; i < 100 && !received; i++)
                    {
                        pub.SendFrame("hello");
                        received = sub.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string? _);
                    }

                    Assert.True(received, "Listener stopped accepting connections after a flood of immediately-reset connects");
                }
            }
        }
    }
}
