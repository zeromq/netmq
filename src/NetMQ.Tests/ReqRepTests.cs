using System.Net.Sockets;
using NetMQ.Sockets;
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
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);

                req.SendFrame("Hi");

                CollectionAssert.AreEqual(new[] { "Hi" }, rep.ReceiveMultipartStrings());

                rep.SendFrame("Hi2");

                CollectionAssert.AreEqual(new[] { "Hi2" }, req.ReceiveMultipartStrings());
            }
        }

        [Test]
        public void SendingTwoRequestsInaRow()
        {            
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendFrame("Hi");

                rep.SkipFrame();

                Assert.Throws<FiniteStateMachineException>(() => req.SendFrame("Hi2"));
            }
        }

        [Test]
        public void ReceiveBeforeSending()
        {            
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Test]
        public void SendMessageInResponeBeforeReceiving()
        {            
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => rep.SendFrame("1"));
            }
        }

        [Test]
        public void SendMultipartMessage()
        {            
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendMoreFrame("Hello").SendFrame("World");

                CollectionAssert.AreEqual(new[] { "Hello", "World" }, rep.ReceiveMultipartStrings());

                rep.SendMoreFrame("Hello").SendFrame("Back");

                CollectionAssert.AreEqual(new[] { "Hello", "Back" }, req.ReceiveMultipartStrings());
            }
        }
    }
}
