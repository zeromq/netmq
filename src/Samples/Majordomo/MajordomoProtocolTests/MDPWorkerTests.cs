using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MajordomoProtocol;
using MDPCommons;

using NetMQ;
using NetMQ.Sockets;
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
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException> (() => new MDPWorker (string.Empty, "test"));
        }

        [Test]
        public void ctor_invalidServerName_ShouldThrowApplicationException ()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException> (() => new MDPWorker ("tcp://127.0.0.1:5555", "   "));
        }

        [Test]
        public void ReceiveImplicitConnect_ValidScenario_ShouldReturnRequest ()
        {
            const string hostAddress = "tcp://localhost:5557";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test", new[] { (byte) '1' }))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    // we expect to receive a 5 Frame message
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
                    e.Socket.SendMultipartMessage (reply);
                };

                poller.Add (broker);
                poller.RunAsync ();

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.Info);
                // initialise the worker - broker protocol
                session.Receive (null);

                Assert.That (loggingMessages.Count, Is.EqualTo (5));
                Assert.That (loggingMessages[0], Is.EqualTo ("[WORKER] connected to broker at tcp://localhost:5557"));
                Assert.That (loggingMessages[1].Contains ("[WORKER] sending"), Is.True);
                Assert.That (loggingMessages[2].Contains ("[WORKER] received"));
                Assert.That (loggingMessages[4].Contains ("abandoning"));
            }
        }

        [Test]
        public void Receive_BrokerDisconnectedWithLogging_ShouldReturnRequest ()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test"))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors but don't answer
                broker.ReceiveReady += (s, e) => e.Socket.ReceiveMultipartMessage ();

                poller.Add (broker);
                poller.RunAsync ();

                // speed up the test
                session.HeartbeatDelay = TimeSpan.FromMilliseconds (250);
                session.ReconnectDelay = TimeSpan.FromMilliseconds (250);
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.Info);
                // initialise the worker - broker protocol
                session.Receive (null);

                poller.Stop ();

                Assert.That (loggingMessages.Count (m => m.Contains ("retrying")), Is.EqualTo (3));
                // 3 times retrying and 1 time initial connecting
                Assert.That (loggingMessages.Count (m => m.Contains ("localhost")), Is.EqualTo (4));
                Assert.That (loggingMessages.Last ().Contains ("abandoning"));
            }
        }

        [Test]
        public void Receive_RequestWithMDPVersionMismatch_ShouldThrowApplicationException ()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication

            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test"))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    // we expect to receive a 5 Frame message
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
                    e.Socket.SendMultipartMessage (reply);
                };

                poller.Add (broker);
                poller.RunAsync ();

                try
                {
                    session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("Invalid protocol header received!"));
                }
            }
        }

        [Test]
        public void Receive_RequestWithWrongFirstFrame_ShouldThrowApplicationException ()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test"))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    // we expect to receive a 5 Frame message
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
                    e.Socket.SendMultipartMessage (reply);
                };

                poller.Add (broker);
                poller.RunAsync ();

                try
                {
                    session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("First frame must be an empty frame!"));
                }
            }
        }

        [Test]
        public void Receive_RequestWithWrongMDPComand_ShouldLogCorrectMessage ()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();
            var first = true;

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test", Encoding.ASCII.GetBytes ("Worker"), 2))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    // we expect to receive a 5 Frame message
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
                    e.Socket.SendMultipartMessage (reply);
                };
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.Info);

                poller.Add (broker);
                poller.RunAsync ();

                session.HeartbeatDelay = TimeSpan.FromMilliseconds (250);
                session.ReconnectDelay = TimeSpan.FromMilliseconds (250);
                // initialise the worker - broker protocol
                session.Receive (null);

                Assert.That (loggingMessages.Count (m => m.Contains ("[WORKER ERROR] invalid command received")), Is.EqualTo (1));
                Assert.That (loggingMessages.Count (m => m.Contains ("abandoning")), Is.EqualTo (1));
            }
        }

        [Test]
        public void Receive_RequestWithTooLittleFrames_ShouldThrowApplicationException ()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test"))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    // we expect to receive a 5 Frame message
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
                    e.Socket.SendMultipartMessage (reply);
                };

                poller.Add (broker);
                poller.RunAsync ();

                try
                {
                    session.Receive (null);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("Malformed request received!"));
                }
            }
        }

        [Test]
        public void Receive_REPLYtoREQUEST_ShouldSendCorrectReply ()
        {
            const string hostAddress = "tcp://localhost:5557";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var broker = new RouterSocket ())
            using (var poller = new NetMQPoller ())
            using (var session = new MDPWorker (hostAddress, "test", new[] { (byte) 'W', (byte) '1' }))
            {
                broker.Bind (hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage ();
                    if (msg[3].Buffer[0] == (byte) MDPCommand.Ready)
                    {
                        // this is a READY message and we
                        // send REQUEST message
                        var request = new NetMQMessage ();
                        request.Push ("echo test"); // [request]
                        request.Push (NetMQFrame.Empty); // [e][request]
                        request.Push ("C1"); // [client adr][e][request]
                        request.Push (new[] { (byte) MDPCommand.Request }); // [command][client adr][e][request]
                        request.Push (msg[2]); // [header][command][client adr][e][request]
                        request.Push (NetMQFrame.Empty); // [e][header][command][client adr][e][request]
                        request.Push (msg[0]); // [worker adr][e][header][command][client adr][e][request]
                        // send reply which is a request for the worker
                        e.Socket.SendMultipartMessage (request);
                    }

                    if (msg[3].Buffer[0] == (byte) MDPCommand.Reply)
                    {
                        // we expect to receive
                        // [WORKER ADR][e]["MDPW01"][REPLY][CLIENT ADR][e][request == "echo test"]
                        // make sure the frames are as expected
                        Assert.That (msg[0].ConvertToString (), Is.EqualTo ("W1"));
                        Assert.That (msg[1], Is.EqualTo (NetMQFrame.Empty));
                        Assert.That (msg[2].ConvertToString (), Is.EqualTo ("MDPW01"));
                        Assert.That (msg[3].BufferSize, Is.EqualTo (1));
                        Assert.That (msg[3].Buffer[0], Is.EqualTo ((byte) MDPCommand.Reply));
                        Assert.That (msg[4].ConvertToString (), Is.EqualTo ("C1"));
                        Assert.That (msg[5], Is.EqualTo (NetMQFrame.Empty));
                        Assert.That (msg[6].ConvertToString (), Is.EqualTo ("echo test"));

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
                        e.Socket.SendMultipartMessage (reply);
                    }
                };

                poller.Add (broker);
                poller.RunAsync ();

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.Info);
                // initialise the worker - broker protocol
                // and get initial request
                var workerRequest = session.Receive (null);
                // just echo the request
                session.Receive (workerRequest);

                poller.Stop ();

                Assert.That (loggingMessages.Count, Is.EqualTo (8));
                Assert.That (loggingMessages[0], Is.EqualTo ("[WORKER] connected to broker at tcp://localhost:5557"));
                Assert.That (loggingMessages[1].Contains ("Ready"));
                Assert.That (loggingMessages[2].Contains ("[WORKER] received"));
                Assert.That (loggingMessages[3].Contains ("Request"));
                Assert.That (loggingMessages[4].Contains ("Reply"));
                Assert.That (loggingMessages[6].Contains ("Kill"));
                Assert.That (loggingMessages[7].Contains ("abandoning"));
            }
        }
    }
}
