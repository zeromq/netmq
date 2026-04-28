using System.Net;
using System.Net.Sockets;
using System.Text;
using NetMQ.Sockets;
using Xunit;

#if NETFRAMEWORK
using ZeroMQ;
#endif

namespace NetMQ.Tests
{
    public class CurveTests
    {
        [Fact]
        public void CurveTest()
        {
            var serverPair = new NetMQCertificate();
            using var server = new DealerSocket();
            server.Options.CurveServer = true;
            server.Options.CurveCertificate = serverPair;
            int port = server.BindRandomPort("tcp://127.0.0.1");
            
            var clientPair = new NetMQCertificate();
            using var client = new DealerSocket();
            client.Options.CurveServerKey = serverPair.PublicKey;
            client.Options.CurveCertificate = clientPair;
            client.Connect($"tcp://127.0.0.1:{port}");

            for (int i = 0; i < 100; i++)
            {
                client.SendFrame("Hello");
                var hello = server.ReceiveFrameString();
                Assert.Equal("Hello", hello);
                
                server.SendFrame("World");
                var world = client.ReceiveFrameString();
                Assert.Equal("World", world);
            }
            
            
        }
        
#if NETFRAMEWORK
        [Fact]
        public void WithLibzmqClient()
        {
            using (var ctx = new ZContext())
            using (var client = ZSocket.Create(ctx, ZSocketType.DEALER))
            using (var server = new DealerSocket())
            {
                var serverPair = new NetMQCertificate();
                server.Options.CurveServer = true;
                server.Options.CurveCertificate = serverPair;
                int port = server.BindRandomPort("tcp://127.0.0.1");
                
                var clientCert = new ZCert();
                client.CurveSecretKey = clientCert.SecretKey;
                client.CurvePublicKey = clientCert.PublicKey;
                client.CurveServerKey = serverPair.PublicKey;
                client.Connect($"tcp://127.0.0.1:{port}");

                client.SendBytes(Encoding.ASCII.GetBytes("Hello"), 0, 5);
                var hello = server.ReceiveFrameString();
                Assert.Equal("Hello", hello);

                server.SendFrame("Hello");
                var frame = client.ReceiveFrame();
                Assert.Equal("Hello", frame.ReadString());
            }
        }
        
        [Fact]
        public void WithLibzmqServer()
        {
            using (var ctx = new ZContext())
            using (var client = new DealerSocket())
            using (var server = ZSocket.Create(ctx, ZSocketType.DEALER))
            {
                var serverCert = new ZCert();
                server.CurveServer = true;
                server.CurveSecretKey = serverCert.SecretKey;

                // Find a free port dynamically to avoid AddressAlreadyInUseException
                int port;
                using (var temp = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    temp.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    port = ((IPEndPoint)temp.LocalEndPoint).Port;
                }

                server.Bind($"tcp://127.0.0.1:{port}");
                
                var clientKeyPair = new NetMQCertificate();
                client.Options.CurveCertificate = clientKeyPair;
                client.Options.CurveServerKey = serverCert.PublicKey;
                client.Connect($"tcp://127.0.0.1:{port}");

                server.SendBytes(Encoding.ASCII.GetBytes("Hello"), 0, 5);
                var hello = client.ReceiveFrameString();
                Assert.Equal("Hello", hello);

                client.SendFrame("Hello");
                var frame = server.ReceiveFrame();
                Assert.Equal("Hello", frame.ReadString());
            }
        }

#endif
    }
}