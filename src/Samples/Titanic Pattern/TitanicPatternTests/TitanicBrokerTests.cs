using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MajordomoProtocol;
using MajordomoProtocol.Contracts;
using NetMQ;
using NUnit.Framework;

using TitanicCommons;
using TitanicProtocol;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicBrokerTests
    {
        /*
         *  Run 
         *  ProcessTitanicRequest - Thread
         *  ProcessTitanicReply - Thread
         *  ProcessTitanicClose - Thread
         *  
         *  DispatchRequests
         *  ServiceCall
         *  
         *  DidAnyTaskStopp 
         *  LogException
         */

        private const string _titanic_broker_address = "tcp://localhost:5555";
        private const string _titanic_internal_communication = "inproc://titanic.inproc";
        private const string _titanic_directory = ".titanic";
        private const string _titanic_queue = "titanic.queue";
        private const string _request_ending = ".request";
        private const string _reply_ending = ".reply";

        [Test]
        public void ctor_WithStandardPath_SholdNOTCreateRootFileAndQueue ()
        {
            using (ITitanicBroker sut = new TitanicBroker ())
            {
                sut.Should ().NotBeNull ();

                var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
                var queue = Path.Combine (path, _titanic_queue);

                File.Exists (queue).Should ().BeFalse (string.Format ("because {0} should NOT have been created!", queue));

                File.Delete (queue);
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

                cts.Cancel ();

                File.Delete (queue);
            }
        }

        /*
         *  Testing Titanic proofs to be a challenge. Either one simulates the MDPBroker <-> MDPWorker
         *  protocol or set-up a MDPBroker for the communication, use a MDPClient for all communication
         *  and set-up a MDPWorker for the required processing of a request
         *  
         */

        //[Test]
        //public void Run_TitanicRequest_SendRequest_ShouldReplyCorrect ()
        //{
        //    // 1. create and start MDPBroker
        //    // 2. create and start TitanicBroker (will register 3 MDPWorker titanic.request, ~.reply and ~.close)
        //    // 3. create MDPClient
        //    // 4. send request
        //    // 5. test for valid GUID as reply
        //    // 6. test for created file GUID.request & queue must consist GUID as requests
        //    // 7. dispose of all created objects

        //    var workerName = new[] { (byte) 'W', (byte) '1' };
        //    var clientName = new[] { (byte) 'C', (byte) '1' };

        //    using (var mdpBroker = new MDPBroker (_titanic_broker_address))
        //    using (var titanicBroker = new TitanicBroker (_titanic_broker_address))
        //    using (var worker = new MDPWorker (_titanic_broker_address, "echo", workerName))
        //    using (var client = new MDPClient (_titanic_broker_address, clientName))
        //    using (var cts = new CancellationTokenSource ())
        //    {
        //        mdpBroker.LogInfoReady += (s, e) => Console.WriteLine (e.Info);
        //        //mdpBroker.DebugInfoReady += (s, e) => Console.WriteLine (e.Info);
        //        titanicBroker.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

        //        var mdp = Task.Factory.StartNew (() => mdpBroker.Run (cts.Token), cts.Token);
        //        var titanic = Task.Factory.StartNew (() => titanicBroker.Run (), cts.Token);
        //        var workerTask = Task.Factory.StartNew (() => EchoRequestWorker (worker), cts.Token);
        //        // give the system some time to organize itself
        //        Thread.Sleep (500);

        //        var request = new NetMQMessage ();
        //        request.Push ("Hello from Client!");
        //        request.Push ("echo");

        //        var reply = client.Send (TitanicOperation.Request.ToString (), request);

        //        reply.FrameCount.Should ().Be (2, "because only 2 Frames are returned");
        //        reply[0].ConvertToString ().Should ().Be ("Ok", "because OK should be returned");

        //        Guid id;
        //        Guid.TryParse (reply[0].ConvertToString (), out id).Should ().BeTrue ("because a valid GUID should have been returned");

        //        var path = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, _titanic_directory);
        //        var queue = Path.Combine (path, _titanic_queue);
        //        var requestFile = Path.Combine (path, id + _request_ending);

        //        File.Exists (requestFile).Should ().BeTrue ("because {0} should result in {1} exist.", id, requestFile);

        //        var io = new TitanicIO ();
        //        var requestMessage = io.FindRequest (id);
        //        requestMessage.Should ().NotBeNull ("because the entry shold have been created.");

        //        cts.Cancel ();

        //        File.Delete (requestFile);
        //    }
        //}

        //// ==================== Utility Methods for the MDPWorker & MDPClient needed

        //private void RequestClient (string serviceName, string endpoint, int runs = 1)
        //{
        //    var replies = new List<NetMQMessage> ();
        //    var idC01 = new[] { (byte) 'C', (byte) '1' };

        //    var client = new MDPClient (endpoint, idC01);

        //    for (var i = 0; i < runs; i++)
        //    {
        //        var request = new NetMQMessage ();
        //        request.Push (string.Format ("Message #{0}", i));
        //        var reply = client.Send (serviceName, request);
        //        replies.Add (reply);
        //    }

        //    client.Dispose ();
        //}

        //private void EchoRequestWorker (IMDPWorker worker = null, string endpoint = null, int heartbeatinterval = 2500)
        //{
        //    var idW01 = new[] { (byte) 'W', (byte) '1' };

        //    worker = worker ?? new MDPWorker (endpoint, "echo", idW01);

        //    worker.HeartbeatDelay = TimeSpan.FromMilliseconds (heartbeatinterval);

        //    NetMQMessage reply = null;

        //    while (true)
        //    {
        //        // send the reply and wait for a request
        //        var request = worker.Receive (reply);
        //        // was the worker interrupted
        //        if (ReferenceEquals (request, null))
        //            throw new ApplicationException ("Request was <null>!");
        //        // echo the request and wait for next request which will not come
        //        // here the task will be canceled
        //        reply = worker.Receive (request);
        //    }
        //}
    }
}
