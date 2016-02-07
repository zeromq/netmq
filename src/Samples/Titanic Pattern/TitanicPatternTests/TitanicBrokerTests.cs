using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using NUnit.Framework;

using NetMQ;

using TitanicCommons;
using TitanicProtocol;
using TitanicProtocolTests.TestEntities;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicBrokerTests
    {
        private const string _titanic_broker_address = "tcp://localhost:5555";
        private const string _titanic_directory = ".titanic";
        private const string _titanic_queue = "titanic.queue";

        private const int _sleep_for = 200;     // time in milliseconds to delay the processing

        [Test]
        public void ctor_WithStandardPath_ShouldCreateRootAndQueue ()
        {
            using (ITitanicBroker sut = new TitanicBroker ())
            {
                sut.Should ().NotBeNull ();

                var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
                var queue = Path.Combine (path, _titanic_queue);

                File.Exists (queue).Should ().BeTrue (string.Format ("because {0} should NOT have been created!", queue));

                // clean up
                File.Delete (queue);
                Directory.Delete (path);
            }
        }

        [Test]
        public void ctor_WithNoneStandardPath_ShouldCreateRootAndQueueInIndicatedPath ()
        {
            var path = Path.Combine (Path.GetTempPath (), _titanic_directory);
            // create root for Titanic - it expects the root to exist(!)
            Directory.CreateDirectory (path);
            var queue = Path.Combine (path, _titanic_queue);

            using (ITitanicBroker sut = new TitanicBroker (_titanic_broker_address, path))
            {
                sut.Should ().NotBeNull ();

                try
                {
                    File.Exists (queue).Should ().BeTrue (string.Format ("because {0} should have been created!", queue));
                }
                finally
                {
                    // clean up
                    File.Delete (queue);
                    Directory.Delete (path);
                }
            }
        }

        [Test]
        public void Run_CheckStandardConfiguration_ShouldHaveCreatedInfrastructure ()
        {
            using (ITitanicBroker sut = new TitanicBroker ())
            using (var cts = new CancellationTokenSource ())
            {
                var t = Task.Factory.StartNew (() => sut.Run (), cts.Token);
                t.Status.Should ().Be (TaskStatus.WaitingToRun, "because it should wait for a request!");

                var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
                var queue = Path.Combine (path, _titanic_queue);

                Directory.Exists (path).Should ().BeTrue (string.Format ("because {0} should have been created!", path));
                File.Exists (queue).Should ().BeTrue (string.Format ("because {0} should have been created!", queue));

                // clean up
                cts.Cancel ();
                File.Delete (queue);
                Directory.Delete (path);
            }
        }

        [Test]
        public void Run_CheckNoneStandardConfiguration_ShouldHaveCreatedInfrastructure ()
        {
            var path = Path.Combine (Path.GetTempPath (), _titanic_directory);
            // create root for Titanic - it expects the root to exist(!)
            Directory.CreateDirectory (path);
            var queue = Path.Combine (path, _titanic_queue);
            // create queue for Titanic - it expects the queue to exist(!) at that location
            var f = File.Create (queue);
            f.Dispose ();

            using (ITitanicBroker sut = new TitanicBroker (_titanic_broker_address, path))
            using (var cts = new CancellationTokenSource ())
            {
                var t = Task.Factory.StartNew (() => sut.Run (), cts.Token);
                t.Status.Should ().Be (TaskStatus.WaitingToRun, "because it should wait for a request!");

                Directory.Exists (path).Should ().BeTrue (string.Format ("because {0} should have been created!", path));
                File.Exists (queue).Should ().BeTrue (string.Format ("because {0} should have been created!", queue));

                // clean up
                cts.Cancel ();
                File.Delete (queue);
                Directory.Delete (path);
            }
        }

        /*
         * The following tests pass individual but NOT automatic! (and I don't know why :-()
         * 
         * Testing the main methods of TitanicBroker is complex due to the four task
         * used Run/Request/Reply/Close
         * 
         *      Task      incoming              outgoing                   Whereto
         *      Request                         [empty]                    MDPBroker
         *                [service][request]    [Ok][Guid]                 Requesting Party
         *                                      [Guid]                     Titanic.Run
         *      
         *      Reply                           [empty]                    MDPBroker
         *                [Guid]                a) [Ok][service][reply]    Requesting Party
         *                                      b) [Pending]               Requesting Party
         *                                      c) [Unknown]               Requesting Party
         *      
         *      Close                           [empty]                    MDPBroker
         *                [Guid]                [Ok]                       Requesting Party
         * 
         * all of these communication paths must be tested and the communication partners 
         * must be faked in order to test
         * Therefore specific test implementations for the MDPWorker/MDPClient are/is used.
         * They just short circuit the communication and allow to specify the answers to 
         * give inorder to enable positive as well as negative testing.
         * 
         * To avoid any changes or dependencies to the underlying filesystem an in-memory
         * implementation of ITitanicIO (TitanicMemoryIO) is used.
         * 
         * In order to allow the TitanicBroker to start all objects must be supplied.
         */

        [Test]
        public void Run_NoWorkers_ShouldSitAndWaitFor5Seconds ()
        {
            using (var sut = new TitanicBroker (new TitanicMemoryIO ()))
            {
                Task.Factory.StartNew (() => sut.Run ());

                Thread.Sleep (5000);
            }
        }

        [Test]
        public void TitanicClose_RequestToCloseRequest_ShouldDeleteRequestFromQueue ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // setup the queue with a request
                var guid = Guid.NewGuid ();
                // message content is of no importance here
                io.SaveNewRequestEntry (guid, new NetMQMessage ());
                var entry = io.GetRequestEntry (guid);
                io.SaveProcessedRequestEntry (entry);
                // set up the fake client's request
                closeWorker.Request = new NetMQMessage ();
                closeWorker.Request.Push (guid.ToString ());
                // start the TitanicBroker (Close will automatically start and 
                // wait for a signal to proceed with sending the setup request message
                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));
                // signal closeWorker to go ahead
                closeWorker.waitHandle.Set ();
                // give everything some time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                closeWorker.Reply.FrameCount.Should ().Be (1, "because only one frame should have been returned.");
                closeWorker.Reply.First.ConvertToString ().Should ().Be ("Ok", "because 'Ok' should have been send.");

                // TEST QUEUE
                io.ExistsMessage (TitanicOperation.Request, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Reply, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Close, guid).Should ().BeFalse ();
            }
        }

        [Test]
        public void TitanicReply_RequestReplyOk_ShouldSentCorrectReply ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // setup the queue with a request
                var guid = Guid.NewGuid ();
                io.SaveNewRequestEntry (guid, new NetMQMessage ());
                var entry = io.GetRequestEntry (guid);
                // message content is expected to be [service][reply]
                entry.Request.Push ("REPLY DATA");
                entry.Request.Push ("echo");
                io.SaveProcessedRequestEntry (entry);
                // setup the fake replyWorker's request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (guid.ToString ());
                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));
                // signal worker to go ahead
                repWorker.waitHandle.Set ();
                // give everything some time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                repWorker.Reply.FrameCount.Should ().Be (3, "because a 3 frame message is expected. ({0})", repWorker.Reply);
                repWorker.Reply.First.ConvertToString ().Should ().Be ("Ok");
                repWorker.Reply.Should ().Equal (entry.Request);
                // TEST QUEUE
                io.GetRequestEntries (e => e.RequestId == guid).Count ().Should ().Be (1);
                var queueEntry = io.GetRequestEntry (guid);
                queueEntry.State.Should ().Be (RequestEntry.Is_Processed);
                io.ExistsMessage (TitanicOperation.Close, guid).Should ().BeFalse ();
            }
        }

        [Test]
        public void TitanicReply_RequestReplyPending_ShouldSentCorrectReply ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // setup the queue with a request
                var guid = Guid.NewGuid ();
                var msg = new NetMQMessage ();
                msg.Push (guid.ToString ());
                // queue is setup with a request pending
                io.SaveNewRequestEntry (guid, msg);
                // setup the fake replyWorker's request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (guid.ToString ());
                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));
                // signal worker to go ahead
                repWorker.waitHandle.Set ();
                // give everything some time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                repWorker.Reply.FrameCount.Should ().Be (1, "because a 1 frame message is expected. ({0})", repWorker.Reply);
                repWorker.Reply.First.ConvertToString ().Should ().Be ("Pending");
                // TEST QUEUE
                io.GetRequestEntries (e => e.RequestId == guid).Count ().Should ().Be (1);
                var queueEntry = io.GetRequestEntry (guid);
                queueEntry.State.Should ().Be (RequestEntry.Is_Pending);
                io.ExistsMessage (TitanicOperation.Request, guid).Should ().BeTrue ();
                io.ExistsMessage (TitanicOperation.Reply, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Close, guid).Should ().BeFalse ();
            }
        }

        [Test]
        public void TitanicReply_RequestReplyUnknown_ShouldSentCorrectReply ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // fill queue with requests pending/processed/closed
                for (var i = 0; i < 10; i++)
                {
                    var entry = new RequestEntry
                                {
                                    RequestId = Guid.NewGuid (),
                                    Request = new NetMQMessage (),
                                    State = i % 2 == 0 ? RequestEntry.Is_Pending : RequestEntry.Is_Processed
                                };

                    if (i % 3 == 0)
                        entry.State = RequestEntry.Is_Closed;

                }
                // setup the queue with a request
                var guid = Guid.NewGuid ();
                // setup the fake replyWorker's request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (guid.ToString ());
                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));
                // signal worker to go ahead
                repWorker.waitHandle.Set ();
                // give everything some time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                repWorker.Reply.FrameCount.Should ().Be (1, "because a 1 frame message is expected. ({0})", repWorker.Reply);
                repWorker.Reply.First.ConvertToString ().Should ().Be ("Unknown");
                // TEST QUEUE
                io.ExistsMessage (TitanicOperation.Request, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Reply, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Close, guid).Should ().BeFalse ();
            }
        }

        [Test]
        public void TitanicReply_RequestNonExistingReplyUnknown_ShouldSentCorrectReply ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // setup the queue with a request
                var guid = Guid.NewGuid ();
                // setup the fake replyWorker's request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (guid.ToString ());
                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));
                // signal worker to go ahead
                repWorker.waitHandle.Set ();
                // give everything some time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                repWorker.Reply.FrameCount.Should ().Be (1, "because a 1 frame message is expected. ({0})", repWorker.Reply);
                repWorker.Reply.First.ConvertToString ().Should ().Be ("Unknown");
                // TEST QUEUE
                io.ExistsMessage (TitanicOperation.Request, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Reply, guid).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Close, guid).Should ().BeFalse ();
            }
        }

        [Test]
        public void Run_RequestProcessStandardFlow_ShouldWriteQueueAndReplyCorrect ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // request worker will receive two calls
                //      a) null -> initial call which is answered with a request [service][data]
                //      b) reply -> [Ok][Guid]
                // and TitanicRequest will also write the TitanicQueue -> add a request
                // Run will also write queue with different data
                reqWorker.Request = new NetMQMessage ();
                reqWorker.Request.Push ("Request Data");
                reqWorker.Request.Push ("echo");

                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));

                // give system time to act
                Thread.Sleep (200);

                // TEST COMMUNICATION
                reqWorker.Reply.FrameCount.Should ().Be (2, "because it was {0}", reqWorker.Reply.ToString ());
                reqWorker.Reply.First.ConvertToString ().Should ().Be ("Ok");
                var s = reqWorker.Reply.Last.ConvertToString ();

                Guid id;
                Guid.TryParse (s, out id).Should ().BeTrue ();

                // TEST QUEUE (Run was sent the Guid and it kick started the Dispatch(!)
                io.NumberOfRequests.Should ().Be (1, "because only one request was received");
                var request = io.GetRequestEntry (id);
                request.Should ().NotBe (default (RequestEntry), "because the id for the request should allow the retrieval");
                request.RequestId.Should ().Be (id);
                request.State.Should ().Be (RequestEntry.Is_Pending, "because it has not yet been processed");
                request.Request.Should ().Equal (reqWorker.Request, "because {0} was sent.", reqWorker.Request);
            }
        }

        [Test]
        public void Run_RequestReply_ShouldWriteQueueAndReplyCorrect ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // 1. Request
                // 2. Reply -> test
                reqWorker.Request = new NetMQMessage ();
                reqWorker.Request.Push ("Request Data");
                reqWorker.Request.Push ("echo");

                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));

                // give system time to act
                Thread.Sleep (_sleep_for);

                // get reply id for further processing
                var s = reqWorker.Reply.Last.ConvertToString ();
                Guid id;
                Guid.TryParse (s, out id).Should ().BeTrue ();
                // inject to repWorker who is waiting to proceed and prepare request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (s);
                // get RequestEntry from queue and mark as processed
                var entry = io.GetRequestEntry (id);
                io.SaveProcessedRequestEntry (entry);
                // signal to proceed to send prepared request
                repWorker.waitHandle.Set ();

                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                repWorker.Reply.FrameCount.Should ().Be (3, "because a 3 frame message is expected. ({0})", repWorker.Reply);
                repWorker.Reply.First.ConvertToString ().Should ().Be ("Ok");
                // should be identical since it is an "echo" service we are simulating :-)
                repWorker.Reply.Should ().Equal (entry.Request);
                // TEST QUEUE
                io.NumberOfRequests.Should ().Be (1);
                io.GetRequestEntries (e => e.RequestId == id).Count ().Should ().Be (1);
                var queueEntry = io.GetRequestEntry (id);
                queueEntry.State.Should ().Be (RequestEntry.Is_Processed);
                io.ExistsMessage (TitanicOperation.Close, id).Should ().BeFalse ();

            }
        }

        [Test]
        public void Run_RequestReplyClose_ShouldWriteQueueAndReplyCorrect ()
        {
            var io = new TitanicMemoryIO ();

            using (var reqWorker = new FakeRequestMDPWorker ())
            using (var repWorker = new FakeReplyMDPWorker ())
            using (var closeWorker = new FakeCloseMDPWorker ())
            using (var dispatchClient = new FakeDispatchMDPClient ())
            using (var sut = new TitanicBroker (io))
            {
                // 1. Request
                // 2. Reply -> test
                reqWorker.Request = new NetMQMessage ();
                reqWorker.Request.Push ("Request Data");
                reqWorker.Request.Push ("echo");

                // start the process chain - worker & client should only run until they hit an AutoResetEvent
                Task.Factory.StartNew (() => sut.Run (reqWorker, repWorker, closeWorker, dispatchClient));

                // give system time to act
                Thread.Sleep (_sleep_for);

                // get reply id for further processing
                var s = reqWorker.Reply.Last.ConvertToString ();
                Guid id;
                Guid.TryParse (s, out id).Should ().BeTrue ();
                // inject to repWorker who is waiting to proceed and prepare request
                repWorker.Request = new NetMQMessage ();
                repWorker.Request.Push (s);
                // get RequestEntry from queue and mark as processed
                var entry = io.GetRequestEntry (id);
                io.SaveProcessedRequestEntry (entry);
                // signal to proceed to send prepared request
                repWorker.waitHandle.Set ();

                // set up the fake client's request
                closeWorker.Request = new NetMQMessage ();
                closeWorker.Request.Push (id.ToString ());
                // signal to proceed to send prepared request
                closeWorker.waitHandle.Set ();

                // give it time to process
                Thread.Sleep (_sleep_for);

                // TEST COMMUNICATION
                closeWorker.Reply.FrameCount.Should ().Be (1, "because only one frame should have been returned.");
                closeWorker.Reply.First.ConvertToString ().Should ().Be ("Ok", "because 'Ok' should have been send.");

                // TEST QUEUE
                io.NumberOfRequests.Should ().Be (0);
                io.ExistsMessage (TitanicOperation.Request, id).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Reply, id).Should ().BeFalse ();
                io.ExistsMessage (TitanicOperation.Close, id).Should ().BeFalse ();
            }
        }
    }
}
