using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MajordomoProtocol;
using MajordomoProtocol.Contracts;

using NetMQ;

using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPWorkerTests
    {
        [Test]
        public void ctor_ValidParameter_ShouldReturnWorker ()
        {
            var session = new MDPWorker ("tcp://127.0.0.1:5555", "test");

            Assert.That (session, Is.Not.Null);
            Assert.That (session.HeartbeatDelay, Is.EqualTo (TimeSpan.FromMilliseconds (2500)));
            Assert.That (session.ReconnectDelay, Is.EqualTo (TimeSpan.FromMilliseconds (2500)));
        }

        [Test]
        public void ctor_InvalidBrokerAddress_ShouldThrowApplicationException ()
        {
            Assert.Throws<ArgumentNullException> (() => new MDPWorker (string.Empty, "test"));
        }

        [Test]
        public void ctor_invalidServerName_ShouldThrowApplicationException ()
        {
            Assert.Throws<ArgumentNullException> (() => new MDPWorker ("tcp://127.0.0.1:5555", "   "));
        }

        [Test]
        public void ReceiveImplicitConnect_ValidScenario_ShouldReturnRequest ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test"))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMessage ();
                    // we expect to receive a 5 Frame mesage
                    // [WORKER ADR][EMPTY]["MDPW01"]["READY"]["test"]
                    if (msg.FrameCount != 5)
                        Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                    // make sure the frames are as expected
                    Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                    Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                    Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                    Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Ready));
                    Assert.That (msg[4].ConvertToString (), Is.EqualTo ("test"));

                    // tell worker to stop gracefully
                    var reply = new NetMQMessage ();
                    reply.Push (new[] { (byte) MDPCommand.Kill });
                    // push MDP Version
                    reply.Push (msg[2]);
                    // push separator
                    reply.Push (NetMQFrame.Empty);
                    // push worker address
                    reply.Push (msg[0]);
                    // send reply which is a request for the worker
                    e.Socket.SendMessage (reply);
                };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // initialize the worker - broker protocol
                session.Receive (null);

                poller.Stop ();
                poller.RemoveSocket (broker);

                Assert.That (loggingMessages.Count, Is.EqualTo (4));
                Assert.That (loggingMessages[0], Is.EqualTo ("[WORKER] connecting to broker at tcp://localhost:5555"));
                Assert.That (loggingMessages[1].Contains ("[WORKER] sending"), Is.True);
                Assert.That (loggingMessages[2].Contains ("[WORKER] request received"));
                Assert.That (loggingMessages[3].Contains ("abandoning"));
            }
        }

        [Test]
        public void Receive_BrokerDisconnectedWithLogging_ShouldReturnRequest ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test"))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors but don't answer
                broker.ReceiveReady += (s, e) => e.Socket.ReceiveMessage ();

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                // speed up the test
                session.HeartbeatDelay = TimeSpan.FromMilliseconds (250);
                session.ReconnectDelay = TimeSpan.FromMilliseconds (250);
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // initialize the worker - broker protocol
                session.Receive (null);

                poller.Stop ();
                poller.RemoveSocket (broker);

                Assert.That (loggingMessages.Count (m => m.Contains ("retrying")), Is.EqualTo (3));
                // 3 times retrying and 1 time initial connecting
                Assert.That (loggingMessages.Count (m => m.Contains ("localhost")), Is.EqualTo (4));
                Assert.That (loggingMessages.Last ().Contains ("abandoning"));
            }
        }

        [Test]
        public void Receive_RequestWithMDPVersionMismatch_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test"))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                                       {
                                           var msg = e.Socket.ReceiveMessage ();
                                           // we expect to receive a 5 Frame mesage
                                           // [WORKER ADR][EMPTY]["MDPW01"]["READY"]["test"]
                                           if (msg.FrameCount != 5)
                                               Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                                           // make sure the frames are as expected
                                           Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                                           Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                                           Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                                           Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Ready));
                                           Assert.That (msg[4].ConvertToString (), Is.EqualTo ("test"));

                                           // tell worker to stop gracefully
                                           var reply = new NetMQMessage ();
                                           reply.Push (new[] { (byte) MDPCommand.Kill });
                                           // push MDP Version
                                           reply.Push ("MDPW00");
                                           // push separator
                                           reply.Push (NetMQFrame.Empty);
                                           // push worker address
                                           reply.Push (msg[0]);
                                           // send reply which is a request for the worker
                                           e.Socket.SendMessage (reply);
                                       };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                try
                {
                    var reply = session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("Invalid protocol header received"));
                }

                poller.Stop ();
                poller.RemoveSocket (broker);
            }
        }

        [Test]
        public void Receive_RequestWithWrongFirstFrame_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test"))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                                       {
                                           var msg = e.Socket.ReceiveMessage ();
                                           // we expect to receive a 5 Frame mesage
                                           // [WORKER ADR][EMPTY]["MDPW01"]["READY"]["test"]
                                           if (msg.FrameCount != 5)
                                               Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                                           // make sure the frames are as expected
                                           Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                                           Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                                           Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                                           Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Ready));
                                           Assert.That (msg[4].ConvertToString (), Is.EqualTo ("test"));

                                           // tell worker to stop gracefully
                                           var reply = new NetMQMessage ();
                                           reply.Push (new[] { (byte) MDPCommand.Kill });
                                           // push MDP Version
                                           reply.Push ("MDPW01");
                                           // push separator
                                           reply.Push ("Should be empty");
                                           // push worker address
                                           reply.Push (msg[0]);
                                           // send reply which is a request for the worker
                                           e.Socket.SendMessage (reply);
                                       };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                try
                {
                    var reply = session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("First frame must be an empty frame!"));
                }

                poller.Stop ();
                poller.RemoveSocket (broker);
            }
        }

        [Test]
        public void Receive_RequestWithWrongMDPComand_ShouldLogCorretMessage ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();
            var first = true;

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test", 2))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMessage ();
                    // we expect to receive a 5 Frame mesage
                    // [WORKER ADR][EMPTY]["MDPW01"]["READY"]["test"]
                    if (msg.FrameCount != 5)
                        return; // it is a HEARTBEAT
                    // make sure the frames are as expected
                    Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                    Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                    Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                    Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Ready));
                    Assert.That (msg[4].ConvertToString (), Is.EqualTo ("test"));

                    // tell worker to stop gracefully
                    var reply = new NetMQMessage ();
                    if (first)
                    {
                        reply.Push (new[] { (byte) 0xff });
                        first = false;
                    }
                    else
                        reply.Push (new[] { (byte) MDPCommand.Kill });
                    // push MDP Version
                    reply.Push ("MDPW01");
                    // push separator
                    reply.Push (NetMQFrame.Empty);
                    // push worker address
                    reply.Push (msg[0]);
                    // send reply which is a request for the worker
                    e.Socket.SendMessage (reply);
                };
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                session.HeartbeatDelay = TimeSpan.FromMilliseconds (250);
                session.ReconnectDelay = TimeSpan.FromMilliseconds (250);
                // initialize the worker - broker protocol
                session.Receive (null);

                Assert.That (loggingMessages.Count (m => m.Contains ("[WORKER ERROR] invalid command received")), Is.EqualTo (1));
                Assert.That (loggingMessages.Count (m => m.Contains ("abandoning")), Is.EqualTo (1));

                poller.Stop ();
                poller.RemoveSocket (broker);
            }
        }

        [Test]
        public void Receive_RequestWithTooLittleFrames_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPWorker (host_address, "test"))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMessage ();
                    // we expect to receive a 5 Frame mesage
                    // [WORKER ADR][EMPTY]["MDPW01"]["READY"]["test"]
                    if (msg.FrameCount != 5)
                        Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                    // make sure the frames are as expected
                    Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                    Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                    Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                    Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Ready));
                    Assert.That (msg[4].ConvertToString (), Is.EqualTo ("test"));

                    // tell worker to stop gracefully
                    var reply = new NetMQMessage ();
                    reply.Push (new[] { (byte) MDPCommand.Kill });
                    // push separator
                    reply.Push (NetMQFrame.Empty);
                    // push worker address
                    reply.Push (msg[0]);
                    // send reply which is a request for the worker
                    e.Socket.SendMessage (reply);
                };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                try
                {
                    var reply = session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("Malformed request received"));
                }

                poller.Stop ();
                poller.RemoveSocket (broker);
            }
        }
    }
}
