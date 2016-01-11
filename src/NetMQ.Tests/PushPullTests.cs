using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class PushPullTests
    {
        [Test]
        public void SimplePushPull()
        {            
            using (var pullSocket = new PullSocket())
            using (var pushSocket = new PushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.SendFrame("hello");

                Assert.AreEqual("hello", pullSocket.ReceiveFrameString());
            }
        }

        [Test]
        public void EmptyMessage()
        {            
            using (var pullSocket = new PullSocket())
            using (var pushSocket = new PushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.SendFrame(new byte[300]);

                Assert.AreEqual(300, pullSocket.ReceiveFrameString().Length);
            }
        }
    }
}
