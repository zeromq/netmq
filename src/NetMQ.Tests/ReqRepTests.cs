using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ReqRepTests
    {
        [Test]
        [TestCase("tcp://localhost")]
        [TestCase("tcp://127.0.0.1")]
        [TestCase("tcp://unknownhostname", ExpectedException = typeof(System.Net.Sockets.SocketException))]
        public void SimpleReqRep(string address)
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            {
                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);

                req.Send("Hi");

                bool more;

                Assert.AreEqual("Hi", rep.ReceiveString(out more));
                Assert.IsFalse(more);

                rep.Send("Hi2");

                Assert.AreEqual("Hi2", req.ReceiveString(out more));
                Assert.IsFalse(more);
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

                rep.Receive();

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

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveString());
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

                bool more;

                Assert.AreEqual("Hello", rep.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("World", rep.ReceiveString(out more));
                Assert.IsFalse(more);

                rep.SendMore("Hello").Send("Back");

                Assert.AreEqual("Hello", req.ReceiveString(out more));
                Assert.IsTrue(more);

                Assert.AreEqual("Back", req.ReceiveString(out more));
                Assert.IsFalse(more);
            }
        }
    }
}
