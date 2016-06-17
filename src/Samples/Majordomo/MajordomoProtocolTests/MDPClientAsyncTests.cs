using System;
using System.Collections.Generic;
using MajordomoProtocol;
using NetMQ;
using NetMQ.Sockets;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPClientAsyncTests
    {
        [Test]
        public void ContructorShouldReturnMDPClientAsync()
        {
            var address = "tcp://localhost:5555";
            var session = new MDPClientAsync(address);

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Address, Is.EqualTo(address));
            Assert.That(session.Timeout, Is.EqualTo(TimeSpan.FromMilliseconds(10000)));

            session.Dispose();
        }

        [Test]
        public void ContructorWithNoBrokerAddressShouldThrowError()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new MDPClientAsync(string.Empty));
        }

        [Test]
        public void ContructorWithWhitespaceBrokerAddressShouldThrowError()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new MDPClientAsync("   "));
        }

        [Test]
        public void SendCorrectInputWithLoggingShouldReturnCorrectReply()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();
            var serviceName = "echo";
           
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClientAsync(hostAddress))
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

                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                session.ReplyReady += (s, e) =>
                {
                    var reply = e.Reply;

                    Assert.That(reply.FrameCount, Is.EqualTo(1));
                    Assert.That(reply.First.ConvertToString(), Is.EqualTo("REQUEST OK"));
    
                    poller.Stop();
                };
                int timeOutInMillis = 10000;
                var timer = new NetMQTimer(timeOutInMillis); // Used so it doesn't block if something goes wrong!
                timer.Elapsed += (s, e) =>
                {
                    Assert.Fail($"Waited {timeOutInMillis} and had no response from broker");
                    poller.Stop();
                };

                poller.Add(broker);
                poller.Add(timer);
                var task = Task.Factory.StartNew(() => poller.Run());

                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });

                session.Send(serviceName, requestMessage);

                task.Wait();

                Assert.That(loggingMessages.Count, Is.EqualTo(3));
                Assert.That(loggingMessages[0], Is.EqualTo("[CLIENT] connecting to broker at tcp://localhost:5555"));
                Assert.That(loggingMessages[1].Contains("[CLIENT INFO] sending"), Is.True);
                Assert.That(loggingMessages[2].Contains("[CLIENT INFO] received"), Is.True);
            }
        }

        [Test]
        public void SendNoServiceNameWithLoggingShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var session = new MDPClientAsync(hostAddress))
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
        public void SendWithSpaceServiceNameWithLoggingShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var session = new MDPClientAsync(hostAddress))
            {
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
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
        public void SendEmptyReplyFromBrokerWithLoggingShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";
            var loggingMessages = new List<string>();

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClientAsync(hostAddress))
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

                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                session.ReplyReady += (s, e) =>
                {
                    Assert.That(loggingMessages.Count, Is.EqualTo(3));
                    Assert.That(loggingMessages[0], Is.EqualTo("[CLIENT] connecting to broker at tcp://localhost:5555"));
                    Assert.That(loggingMessages[1].Contains("[CLIENT INFO] sending"), Is.True);
                    Assert.That(loggingMessages[2].Contains("[CLIENT INFO] received"), Is.True);

                    poller.Stop(); // To unlock the Task.Wait()
                };

                poller.Add(broker);
                var task = Task.Factory.StartNew(() => poller.Run());
                // well formed message
                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                // correct call
                session.Send("echo", requestMessage);

                int timeOutInMillis = 10000;
                var timer = new NetMQTimer(timeOutInMillis); // Used so it doesn't block if something goes wrong!
                timer.Elapsed += (s, e) => 
                {
                    Assert.Fail($"Waited {timeOutInMillis} and had no response from broker");
                    poller.Stop();
                };

                poller.Add(timer);
                task.Wait();
            }
        }

        [Test]
        public void SendWrongMDPVersionFromBrokerNoLoggingShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClientAsync(hostAddress))
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

                int timeOutInMillis = 10000;
                var timer = new NetMQTimer(timeOutInMillis); // Used so it doesn't block if something goes wrong!
                timer.Elapsed += (s, e) =>
                {
                    Assert.Fail($"Waited {timeOutInMillis} and had no response from broker");
                    poller.Stop();
                };

                poller.Add(broker);
                poller.Add(timer);

                session.ReplyReady += (s, e) =>
                {
                    Assert.True(e.HasError());
                    Assert.That(e.Exception.Message, Is.StringContaining("MDP Version mismatch"));
                    poller.Stop(); // To unlock the Task.Wait()
                };

                var task = Task.Factory.StartNew(() => poller.Run());

                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                session.Send("echo", requestMessage);

                task.Wait();
            }
        }

        [Test]
        public void SendWrongHeaderFromBrokerNoLoggingShouldThrowApplicationException()
        {
            const string hostAddress = "tcp://localhost:5555";

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClientAsync(hostAddress))
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

                int timeOutInMillis = 10000;
                var timer = new NetMQTimer(timeOutInMillis); // Used so it doesn't block if something goes wrong!
                timer.Elapsed += (s, e) =>
                {
                    Assert.Fail($"Waited {timeOutInMillis} and had no response from broker");
                    poller.Stop();
                };

                poller.Add(broker);
                poller.Add(timer);

                session.ReplyReady += (s, e) =>
                {
                    Assert.True(e.HasError());
                    Assert.That(e.Exception.Message, Is.EqualTo("[CLIENT INFO] answered by wrong service: NoService"));
                    poller.Stop(); // To unlock the Task.Wait()
                };

                var task = Task.Factory.StartNew(() => poller.Run());

                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });
                session.Send("echo", requestMessage);

                task.Wait();
            }
        }

        [Test]
        public void ReconnectToBrokerIfIsNotReplying()
        {
            const string hostAddress = "tcp://localhost:5555";
            const int timeOutToReconnectInMillis = 7500;
            var loggingMessages = new List<string>();
            var serviceName = "echo";
            var messagesReceivedOnBroker = 0;

            // setup the counter socket for communication
            using (var broker = new RouterSocket())
            using (var poller = new NetMQPoller())
            using (var session = new MDPClientAsync(hostAddress))
            { 
                session.Timeout = TimeSpan.FromMilliseconds(timeOutToReconnectInMillis);
                broker.Bind(hostAddress);
                broker.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();
                    if (messagesReceivedOnBroker != 0) // doesn't respond if is the first message received! 
                    {
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
                    }
                    messagesReceivedOnBroker++;
                };

                session.LogInfoReady += (s, e) => loggingMessages.Add(e.Info);
                session.ReplyReady += (s, e) =>
                {
                    var reply = e.Reply;

                    Assert.That(reply.FrameCount, Is.EqualTo(1));
                    Assert.That(reply.First.ConvertToString(), Is.EqualTo("REQUEST OK"));
 
                    poller.Stop();
                };

                var requestMessage = new NetMQMessage(new[] { new NetMQFrame("REQUEST") });

                int timeOutInMillis = timeOutToReconnectInMillis + 3000; // Waits for the timeOut on the client
                var timer = new NetMQTimer(timeOutInMillis);
                timer.Elapsed += (s, e) =>
                {
                    session.Send(serviceName, requestMessage); // resends the request after timeout
                };

                poller.Add(timer);
                poller.Add(broker);
                var task = Task.Factory.StartNew(() => poller.Run());

                session.Send(serviceName, requestMessage);

                task.Wait();

                var numberOfConnects = loggingMessages.FindAll(x => x.Contains("[CLIENT] connecting to broker")).Count;
                Assert.IsTrue(numberOfConnects > 1);
            }
        }
    }
}
