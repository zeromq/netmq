using System.Net.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ReqRepTests
    {
        [Test]
        [TestCase("tcp://localhost")]
        [TestCase("tcp://127.0.0.1")]
        [TestCase("tcp://unknownhostname", ExpectedException = typeof(SocketException))]
        public void SimpleReqRep(string address)
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);

                req.Send("Hi");

                CollectionAssert.AreEqual(new[] { "Hi" }, rep.ReceiveMultipartStrings());

                rep.Send("Hi2");

                CollectionAssert.AreEqual(new[] { "Hi2" }, req.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void SendingTwoRequestsInaRow()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.Send("Hi");

                rep.SkipFrame();

                Assert.Throws<FiniteStateMachineException>(() => req.Send("Hi2"));
            }
        }

        [Test]
        public void ReceiveBeforeSending()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Test]
        public void SendMessageInResponeBeforeReceiving()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => rep.Send("1"));
            }
        }

        [Test]
        public void SendMultipartMessage()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendMore("Hello").Send("World");

                CollectionAssert.AreEqual(new[] { "Hello", "World" }, rep.ReceiveMultipartStrings());

                rep.SendMore("Hello").Send("Back");

                CollectionAssert.AreEqual(new[] { "Hello", "Back" }, req.ReceiveMultipartStrings());
            }
        }
    }
}
