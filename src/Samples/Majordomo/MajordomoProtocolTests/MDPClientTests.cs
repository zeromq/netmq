using System;
using System.Collections.Generic;
using MajordomoProtocol;
using NetMQ;
using NetMQ.Sockets;
using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPClientTests
    {
        [Test]
        public void ctor_NewUp_ShouldReturnMDPClient()
        {
            var session = new MDPClient("tcp://localhost:5555");

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Retries, Is.EqualTo(3));
            Assert.That(session.Timeout, Is.EqualTo(TimeSpan.FromMilliseconds(2500)));

            session.Dispose();
        }

        [Test]
        public void ctor_NoBrokerAddress_ShouldReturnMDPClient()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new MDPClient(string.Empty));
        }

        [Test]
        public void ctor_WhitespaceBrokerAddress_ShouldReturnMDPClient()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new MDPClient("   "));
        }

        [Test]
        public void Send_CorrectInputWithLogging_ShouldReturnCorrectReply()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClient(hostAddress))
            {
                broker.Bind(hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>

                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    // we expect to receive a 4 Frame message
                    // [client adrR][e][mdp header][service][request]
                    if (msg.FrameCount != 5)
                        Assert.Fail("Message with wrong count of frames {0}", msg.FrameCount);
                    // REQUEST socket will strip the his address + empty frame
                    // ROUTER has to add the address prelude in order to identify the correct socket(!)
                    // [client adr][e][mdp header][service][reply]
                    var request = msg.Last.ConvertToString(); // get the request string
                    msg.RemoveFrame(msg.Last); // remove the request frame
                    msg.Append(new NetMQFrame(request + " OK")); // append the reply frame
                    e.Socket.SendMultipartMessage(msg);
                };

                poller.Add(broker);
                poller.RunAsync();
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // correct call
                var reply = session.Send("echo", requestMessage);

                poller.Stop();

                Assert.That(reply.FrameCount, Is.EqualTo(1));
                Assert.That(reply.First.ConvertToString(), Is.EqualTo("REQUEST OK"));
                Assert.That(loggingMessages.Count, Is.EqualTo(3));
                Assert.That(loggingMessages[0], Is.EqualTo("[CLIENT] connecting to broker at tcp://localhost:5555"));
                Assert.That(loggingMessages[1].Contains("[CLIENT INFO] sending"), Is.True);
                Assert.That(loggingMessages[2].Contains("[CLIENT INFO] received"), Is.True);
            }
        }

        [Test]
        public void Send_NoServiceNameWithLogging_ShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var session = new MDPClient(hostAddress))
            {
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // correct call
                try
                {
                    session.Send(string.Empty, requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("serviceName must not be empty or null."));
                }

                Assert.That(loggingMessages.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void Send_WithspaceServiceNameWithLogging_ShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var session = new MDPClient(hostAddress))
            {
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // correct call
                try
                {
                    session.Send("  ", requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("serviceName must not be empty or null."));
                }

                Assert.That(loggingMessages.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void Send_WrongServiceNameWithLogging_ShouldLogPermanentError()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClient(hostAddress))
            {
                broker.Bind(hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    // just swallow message -> wrong service name
                    e.Socket.ReceiveMultipartMessage();
                };

                poller.Add(broker);
                poller.RunAsync();

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // wrong service name
                session.Send("xyz", requestMessage);

                poller.Stop();

                Assert.That(loggingMessages.Count, Is.EqualTo(7));
                Assert.That(loggingMessages[6], Is.EqualTo("[CLIENT ERROR] permanent error, abandoning!"));
            }
        }

        [Test]
        public void Send_EmptyReplyFromBrokerWithLogging_ShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClient(hostAddress))
            {
                broker.Bind(hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    // return empty reply
                    var msg = e.Socket.ReceiveMultipartMessage();
                    // we expect to receive a 4 Frame message
                    // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                    if (msg.FrameCount != 5)
                        Assert.Fail("Message with wrong count of frames {0}", msg.FrameCount);
                    // REQUEST socket will strip the his address + empty frame
                    // ROUTER has to add the address prelude in order to identify the correct socket(!)
                    // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                    e.Socket.SendMultipartMessage(msg);
                };

                poller.Add(broker);
                poller.RunAsync();

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // correct call
                session.Send("echo", requestMessage);

                poller.Stop();

                Assert.That(loggingMessages.Count, Is.EqualTo(3));
                Assert.That(loggingMessages[0], Is.EqualTo("[CLIENT] connecting to broker at tcp://localhost:5555"));
                Assert.That(loggingMessages[1].Contains("[CLIENT INFO] sending"), Is.True);
                Assert.That(loggingMessages[2].Contains("[CLIENT INFO] received"), Is.True);
            }
        }

        [Test]
        public void Send_WrongMDPVersionFromBrokerNoLogging_ShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClient(hostAddress))
            {
                broker.Bind(hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    // return empty reply
                    var msg = e.Socket.ReceiveMultipartMessage();
                    // we expect to receive a 4 Frame message
                    // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                    if (msg.FrameCount != 5)
                        Assert.Fail("Message with wrong count of frames {0}", msg.FrameCount);
                    // REQUEST socket will strip the his address + empty frame
                    // ROUTER has to add the address prelude in order to identify the correct socket(!)
                    // [REQ ADR][EMPTY]["MDPC00"]["echo"]["REQUEST"]
                    var clientAddress = msg.Pop();
                    msg.Pop(); // forget empty frame
                    msg.Pop(); // drop the MDP Version Frame
                    msg.Push("MDPC00"); // insert wrong MDP version
                    msg.Push(NetMQFrame.Empty);
                    msg.Push(clientAddress); // reinsert the client's address

                    e.Socket.SendMultipartMessage(msg);
                };

                poller.Add(broker);
                poller.RunAsync();

                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });

                try
                {
                    session.Send("echo", requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That(ex.Message, Is.StringContaining("MDP Version mismatch"));
                }
            }
        }

        [Test]
        public void Send_WrongHeaderFromBrokerNoLogging_ShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClient(hostAddress))
            {
                broker.Bind(hostAddress);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                {
                    // return empty reply
                    var msg = e.Socket.ReceiveMultipartMessage();
                    // we expect to receive a 4 Frame message
                    // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                    if (msg.FrameCount != 5)
                        Assert.Fail("Message with wrong count of frames {0}", msg.FrameCount);
                    // REQUEST socket will strip the his address + empty frame
                    // ROUTER has to add the address prelude in order to identify the correct socket(!)
                    // [REQ ADR][EMPTY]["MDPC00"]["echo"]["REQUEST"]
                    var clientAddress = msg.Pop();
                    msg.Pop(); // forget empty frame
                    var mdpVersion = msg.Pop();
                    msg.Pop(); // drop service name version
                    msg.Push("NoService");
                    msg.Push(mdpVersion);
                    msg.Push(NetMQFrame.Empty);
                    msg.Push(clientAddress); // reinsert the client's address

                    e.Socket.SendMultipartMessage(msg);
                };

                poller.Add(broker);
                poller.RunAsync();

                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });

                try
                {
                    session.Send("echo", requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("[CLIENT INFO] answered by wrong service: NoService"));
                }
            }
        }
    }
}
