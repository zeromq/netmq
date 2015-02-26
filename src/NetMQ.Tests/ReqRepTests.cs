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
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var rep = ctx.CreateResponseSocket())
                {
                    var port = rep.BindRandomPort(address);

                    using (var req = ctx.CreateRequestSocket())
                    {
                        req.Connect(address + ":" + port);

                        req.Send("Hi");

                        bool more;

                        string requestString = rep.ReceiveString(out more);

                        Assert.AreEqual("Hi", requestString);
                        Assert.IsFalse(more);

                        rep.Send("Hi2");

                        string responseString = req.ReceiveString(out more);

                        Assert.AreEqual("Hi2", responseString);
                        Assert.IsFalse(more);
                    }
                }
            }
        }

        [Test]
        public void SendingTwoRequestsInaRow()
        {
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var rep = ctx.CreateResponseSocket())
                {
                    var port = rep.BindRandomPort("tcp://localhost");

                    using (var req = ctx.CreateRequestSocket())
                    {
                        req.Connect("tcp://localhost:" + port);

                        req.Send("Hi");

                        bool more;
                        rep.Receive(out more);

                        var ex = Assert.Throws<FiniteStateMachineException>(() => req.Send("Hi2"));                        
                    }
                }
            }
        }

        [Test]
        public void ReceiveBeforeSending()
        {
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var rep = ctx.CreateResponseSocket())
                {
                    var port = rep.BindRandomPort("tcp://localhost");


                    using (var req = ctx.CreateRequestSocket())
                    {
                        req.Connect("tcp://localhost:" + port);

                        bool more;

                        var ex = Assert.Throws<FiniteStateMachineException>(() => req.ReceiveString(out more));                        
                    }
                }
            }
        }

        [Test]
        public void SendMessageInResponeBeforeReceiving()
        {
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var rep = ctx.CreateResponseSocket())
                {
                    var port = rep.BindRandomPort("tcp://localhost");

                    using (var req = ctx.CreateRequestSocket())
                    {
                        req.Connect("tcp://localhost:" + port);

                        var ex = Assert.Throws<FiniteStateMachineException>(() => rep.Send("1"));                        
                    }
                }
            }
        }

        [Test]
        public void SendMultiplartMessage()
        {
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var rep = ctx.CreateResponseSocket())
                {
                    var port = rep.BindRandomPort("tcp://localhost");

                    using (var req = ctx.CreateRequestSocket())
                    {
                        req.Connect("tcp://localhost:" + port);

                        req.SendMore("Hello").Send("World");

                        bool more;

                        string m1 = rep.ReceiveString(out more);

                        Assert.IsTrue(more);
                        Assert.AreEqual("Hello", m1);

                        string m2 = rep.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("World", m2);

                        rep.SendMore("Hello").Send("Back");

                        string m3 = req.ReceiveString(out more);

                        Assert.IsTrue(more);
                        Assert.AreEqual("Hello", m3);

                        string m4 = req.ReceiveString(out more);

                        Assert.IsFalse(more);
                        Assert.AreEqual("Back", m4);
                    }
                }
            }
        }
    }
}
