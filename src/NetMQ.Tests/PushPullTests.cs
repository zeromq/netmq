using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class PushPullTests
    {
        [Test]
        public void SimplePushPull()
        {
            using (var context = NetMQContext.Create())
            using (var pullSocket = context.CreatePullSocket())
            using (var pushSocket = context.CreatePushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.Send("hello");

                Assert.AreEqual("hello", pullSocket.ReceiveFrameString());
            }
        }

        [Test]
        public void EmptyMessage()
        {
            using (var context = NetMQContext.Create())
            using (var pullSocket = context.CreatePullSocket())
            using (var pushSocket = context.CreatePushSocket())
            {
                var port = pullSocket.BindRandomPort("tcp://127.0.0.1");
                pushSocket.Connect("tcp://127.0.0.1:" + port);

                pushSocket.Send(new byte[300]);

                Assert.AreEqual(300, pullSocket.ReceiveFrameString().Length);
            }
        }
    }
}
