using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class ReqRepTests : IClassFixture<CleanupAfterFixture>
    {
        public ReqRepTests() => NetMQConfig.Cleanup();

        protected void SimpleReqRepSequence(string address, RequestSocket req, ResponseSocket rep)
        {
            var port = rep.BindRandomPort(address);
            req.Connect(address + ":" + port);

            req.SendFrame("Hi");
            Assert.Equal(new[] { "Hi" }, rep.ReceiveMultipartStrings());
            rep.SendFrame("Hi2");
            Assert.Equal(new[] { "Hi2" }, req.ReceiveMultipartStrings());
        }

        protected void SendTwoReqsInSuccession(string address, RequestSocket req, ResponseSocket rep)
        {
            var port = rep.BindRandomPort(address);
            req.Connect(address + ":" + port);
            req.SendFrame("Hi");
            rep.SkipFrame();
        }

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SimpleReqRepSucceeds(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                SimpleReqRepSequence(address, req, rep);
            }
        }

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SimpleReqRepWithCorrelationSucceeds(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                req.Options.Correlate = true;
                SimpleReqRepSequence(address, req, rep);
            }
        }

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SimpleReqRepWithRelaxedSucceeds(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                req.Options.Relaxed = true;
                SimpleReqRepSequence(address, req, rep);
                req.SendFrame("Hi2"); // ick that this not what I wanted.
            }
        }

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SendingTwoRequestsInSuccessionFails(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);
                req.SendFrame("Hi");
                rep.SkipFrame();
                Assert.Throws<FiniteStateMachineException>(() => req.SendFrame("Hi2"));
            }
        }

        [Theory]
        [InlineData("tcp://localhost")]
        [InlineData("tcp://127.0.0.1")]
        public void SendingTwoRequestsInSuccessionWithRelaxedSucceeds(string address)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                req.Options.Relaxed = true;

                var port = rep.BindRandomPort(address);
                req.Connect(address + ":" + port);
                req.SendFrame("Hi");
                rep.SkipFrame();
            }
        }

        [Fact] 
        public void ReceiveBeforeSendingFails()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Fact]
        public void ReceiveBeforeSendingWithRelaxedStillFails()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                req.Options.Relaxed = true;
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Fact]
        public void ReceiveBeforeSendingWithCorrelateStillFails()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                req.Options.Correlate = true;
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => req.ReceiveFrameBytes());
            }
        }

        [Fact]
        public void SendMessageInResponseBeforeReceivingFails()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                Assert.Throws<FiniteStateMachineException>(() => rep.SendFrame("1"));
            }
        }

        // make sure that a single responder sends messages back to the correct requestors.
        [Fact]
        public void SingleResponderSendsCorrectMessagesToMultipleRequestors()
        {
            using (var rep = new ResponseSocket())
            using (var req1 = new RequestSocket())
            using (var req2 = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req1.Connect($"tcp://127.0.0.1:{port}");
                req2.Connect($"tcp://127.0.0.1:{port}");

                req1.SendFrame("From1");
                req2.SendFrame("From2");

                rep.SendFrame(rep.ReceiveFrameString());
                rep.SendFrame(rep.ReceiveFrameString());

                Assert.Equal("From2", req2.ReceiveFrameString());
                Assert.Equal("From1", req1.ReceiveFrameString());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReplyIsForLatestRequestIfRelaxedAndCorrelateEnabled(bool correlate)
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort($"tcp://127.0.0.1");

                req.Connect($"tcp://127.0.0.1:{port}");
                req.Options.Correlate = correlate;
                req.Options.Relaxed = true;
          
                req.SendFrame("Request1");
                req.SendFrame("Request2");

                rep.SendFrame(rep.ReceiveFrameString());
                rep.SendFrame(rep.ReceiveFrameString());

                if (correlate)
                {
                    Assert.Equal("Request2", req.ReceiveFrameString());
                }
                else
                {
                    Assert.Equal("Request1", req.ReceiveFrameString());
                }
            }
        }

        internal void RouterBounce(ref RouterSocket router)
        {
            bool more;
            do
            {
                var bytes = router.ReceiveFrameBytes(out more);
                router.SendFrame(bytes, more);
            } while (more);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CorrelationSelectsLatestRequest(bool correlate)
        {
            var rep = new RouterSocket();
            var req = new RequestSocket();

            var port = rep.BindRandomPort("tcp://localhost");
            req.Connect($"tcp://localhost:{port}");
                
            req.Options.Relaxed = true;
            req.Options.Correlate = correlate;

            //  Send two requests.
            req.SendFrame("FirstReq");
            req.SendFrame("SecondReq");

            //  Bind server allowing it to receive messages.
            //rep.Bind($"tcp://localhost:{port}");

            //  Read the two messages and send them back as is.
            RouterBounce(ref rep);
            RouterBounce(ref rep);
              
            //  Read the reply. When Options.Correlate is active,
            //  "FirstReq" should be ditched and "SecondReq" should be read.  Vice
            //  versa when Options.Corellate is not active.
            var result = req.ReceiveFrameString();

            if (correlate)
            {
                // if correlate is on, we get SecondReq which is the typical desired behavior; this
                // is the last request that was sent.
                Assert.Equal("SecondReq", result); 
            }
            else
            {
                // if correlate is off, we get FirstReq, which is not desired behavior; this is not
                // the last request that was sent.
                Assert.Equal("FirstReq", result); 
            }
            
            rep.Dispose();
            req.Dispose();
        }

        [Fact]
        public void SendMultipartMessageSucceeds()
        {
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://localhost");
                req.Connect("tcp://localhost:" + port);

                req.SendMoreFrame("Hello").SendFrame("World");

                Assert.Equal(new[] { "Hello", "World" }, rep.ReceiveMultipartStrings());

                rep.SendMoreFrame("Hello").SendFrame("Back");

                Assert.Equal(new[] { "Hello", "Back" }, req.ReceiveMultipartStrings());
            }
        }
    }
}
