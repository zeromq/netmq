using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Sockets;
using NUnit;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class NetMQProactorTests
    {
        [Test]
        public void ReceiveMessage()
        {
            using (DealerSocket server = new DealerSocket("@tcp://127.0.0.1:5555"))
            using (DealerSocket client = new DealerSocket(">tcp://127.0.0.1:5555"))
            {
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                NetMQProactor proactor = new NetMQProactor(client, (socket, message) =>
                {
                    manualResetEvent.Set();
                });

                server.SendFrame("Hello");

                Assert.IsTrue(manualResetEvent.WaitOne(100));

                proactor.Dispose();
            }
        }
    }
}
