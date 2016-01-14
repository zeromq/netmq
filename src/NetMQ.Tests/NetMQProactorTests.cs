using System.Threading;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class NetMQProactorTests
    {
        [Test]
        public void ReceiveMessage()
        {
            using (var server = new DealerSocket("@tcp://127.0.0.1:5555"))
            using (var client = new DealerSocket(">tcp://127.0.0.1:5555"))
            using (var manualResetEvent = new ManualResetEvent(false))
            using (new NetMQProactor(client, (socket, message) =>
            {
                manualResetEvent.Set();
                Assert.AreSame(client, socket);
            }))
            {
                server.SendFrame("Hello");

                Assert.IsTrue(manualResetEvent.WaitOne(100));
            }
        }
    }
}
