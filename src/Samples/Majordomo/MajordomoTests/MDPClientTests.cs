using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MajordomoProtocol;
using NetMQ;
using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPClientTests
    {
        [Test]
        public void ctor_NewUp_ShouldReturnMDPClient ()
        {
            var session = new MDPClient ();

            Assert.That (session, Is.Not.Null);
            Assert.That (session.Retries, Is.EqualTo (3));
            Assert.That (session.Timeout, Is.EqualTo (TimeSpan.FromMilliseconds (2500)));

            session.Dispose ();
        }

        [Test]
        public void SendImplicitConnect_NoBrokerAddress_ShouldThrowApplicationException ()
        {
            using (var session = new MDPClient ())
            {
                try
                {
                    session.Send ("echo", new NetMQMessage ());

                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("broker address must not be null or empty!"));
                }
            }
        }

        [Test]
        public void SendImplicitConnect_BrokerAddressEmpty_ShouldThrowApplicationException ()
        {
            using (var session = new MDPClient (string.Empty))
            {
                try
                {
                    session.Send ("echo", new NetMQMessage ());

                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("broker address must not be null or empty!"));
                }
            }
        }

        [Test]
        public void SendImplicitConnect_BrokerAddressWhitespace_ShouldThrowApplicationException ()
        {
            using (var session = new MDPClient ("  "))
            {
                try
                {
                    session.Send ("echo", new NetMQMessage ());

                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("broker address must not be null or empty!"));
                }
            }
        }

        [Test]
        public void SendImplicitConnect_CorrectInputWithLogging_ShouldReturnCorrectReply ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPClient (host_address))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                                       {
                                           var msg = e.Socket.ReceiveMessage ();
                                           // we expect to receive a 4 Frame mesage
                                           // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                                           if (msg.FrameCount != 5)
                                               Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                                           // REQUEST socket will strip the his address + empty frame
                                           // ROUTER has to add the address prelude in order to identify the correct socket(!)
                                           // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]["OK"]
                                           msg.Append ("OK");
                                           e.Socket.SendMessage (msg);
                                       };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // well formed message
                var requestMessage = new NetMQMessage (new[] { new NetMQFrame ("REQUEST") });
                // correct call
                session.Send ("echo", requestMessage);

                poller.Stop ();
                poller.RemoveSocket (broker);

                Assert.That (loggingMessages.Count, Is.EqualTo (3));
                Assert.That (loggingMessages[0], Is.EqualTo ("[CLIENT] connecting to broker at tcp://localhost:5555"));
                Assert.That (loggingMessages[1].Contains ("[CLIENT INFO] sending"), Is.True);
                Assert.That (loggingMessages[2].Contains ("[CLIENT INFO] received"), Is.True);
            }
        }

        [Test]
        public void SendImplicitConnect_NoServiceNameWithLogging_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var session = new MDPClient (host_address))
            {
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // well formed message
                var requestMessage = new NetMQMessage (new[] { new NetMQFrame ("REQUEST") });
                // correct call
                try
                {
                    session.Send (string.Empty, requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("serviceName must not be empty or null."));
                }

                Assert.That (loggingMessages.Count, Is.EqualTo (0));
            }
        }

        [Test]
        public void SendImplicitConnect_WithspaceServiceNameWithLogging_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var session = new MDPClient (host_address))
            {
                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // well formed message
                var requestMessage = new NetMQMessage (new[] { new NetMQFrame ("REQUEST") });
                // correct call
                try
                {
                    session.Send ("  ", requestMessage);
                }
                catch (ApplicationException ex)
                {
                    Assert.That (ex.Message, Is.EqualTo ("serviceName must not be empty or null."));
                }

                Assert.That (loggingMessages.Count, Is.EqualTo (0));
            }
        }

        [Test]
        public void SendImplicitConnect_WrongServiceNameWithLogging_ShouldLogPermanentError ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPClient (host_address))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                                       {
                                           // just swallow message -> wrong service name
                                           var msg = e.Socket.ReceiveMessage ();
                                       };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // well formed message
                var requestMessage = new NetMQMessage (new[] { new NetMQFrame ("REQUEST") });
                // wrong service name
                session.Send ("xyz", requestMessage);

                poller.Stop ();
                poller.RemoveSocket (broker);

                Assert.That (loggingMessages.Count, Is.EqualTo (7));
                Assert.That (loggingMessages[6], Is.EqualTo ("[CLIENT ERROR] permanent error, abandoning!"));
            }
        }

        [Test]
        public void Send_EmptyReplyFromBrokerWithLogging_ShouldThrowApplicationException ()
        {
            const string host_address = "tcp://localhost:5555";
            var loggingMessages = new List<string> ();

            // setup the counter socket for communication
            using (var ctx = NetMQContext.Create ())
            using (var broker = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            using (var session = new MDPClient (host_address))
            {
                broker.Bind (host_address);
                // we need to pick up any message in order to avoid errors
                broker.ReceiveReady += (s, e) =>
                                       {
                                           // return empty reply
                                           var msg = e.Socket.ReceiveMessage ();
                                           // we expect to receive a 4 Frame mesage
                                           // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                                           if (msg.FrameCount != 5)
                                               Assert.Fail ("Message with wrong count of frames {0}", msg.FrameCount);
                                           // REQUEST socket will strip the his address + empty frame
                                           // ROUTER has to add the address prelude in order to identify the correct socket(!)
                                           // [REQ ADR][EMPTY]["MDPC01"]["echo"]["REQUEST"]
                                           e.Socket.SendMessage (msg);
                                       };

                poller.AddSocket (broker);
                var t = Task.Factory.StartNew (() => poller.Start ());

                // set the event handler to receive the logging messages
                session.LogInfoReady += (s, e) => loggingMessages.Add (e.LogInfo);
                // well formed message
                var requestMessage = new NetMQMessage (new[] { new NetMQFrame ("REQUEST") });
                // correct call
                session.Send ("echo", requestMessage);

                poller.Stop ();
                poller.RemoveSocket (broker);

                Assert.That (loggingMessages.Count, Is.EqualTo (3));
                Assert.That (loggingMessages[0], Is.EqualTo ("[CLIENT] connecting to broker at tcp://localhost:5555"));
                Assert.That (loggingMessages[1].Contains ("[CLIENT INFO] sending"), Is.True);
                Assert.That (loggingMessages[2].Contains ("[CLIENT INFO] received"), Is.True);
        }

        [Test]
        public void Send_WrongMDPVersionFromBrokerWithLogging_ShouldThrowApplicationException ()
        {

        }

        [Test]
        public void Send_WrongHeaderFromBrokerWithLogging_ShouldThrowApplicationException ()
        {

        }

        // ========================= helper ========================

        NetMQMessage Build2FrameMessage ()
        {
            var msg = new NetMQMessage ();

            msg.Push (NetMQFrame.Empty);
            msg.Push (NetMQFrame.Empty);

            return msg;
        }

        NetMQMessage BuildMessageWithWrongHeader ()
        {
            var msg = new NetMQMessage ();

            msg.Push ("OK");
            msg.Push ("echo");
            msg.Push ("MDPC00");

            return msg;
        }

        NetMQMessage BuildMessageWithWrongService ()
        {
            var msg = new NetMQMessage ();

            msg.Push ("OK");
            msg.Push ("xyz");
            msg.Push ("MDPC01");

            return msg;
        }

        NetMQMessage BuildCorrectMessage ()
        {
            var msg = new NetMQMessage ();

            msg.Push ("OK");
            msg.Push ("echo");
            msg.Push ("MDPC01");

            return msg;
        }

    }
}
