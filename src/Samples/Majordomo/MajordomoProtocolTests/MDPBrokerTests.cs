using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MajordomoProtocol;
using MDPCommons;

using NetMQ;
using NUnit.Framework;

#pragma warning disable 4014

namespace MajordomoTests
{
    [TestFixture]
    public class MDPBrokerTests
    {
        [Test]
        public void ctor_Simple_ShouldReturnNewObject ()
        {
            var sut = new MDPBroker ("tcp://localhost:5555");

            Assert.That (sut, Is.Not.Null);
            Assert.That (sut.Socket, Is.Not.Null);
            Assert.That (sut.HeartbeatInterval, Is.EqualTo (TimeSpan.FromMilliseconds (2500)));
            Assert.That (sut.HeartbeatLiveliness, Is.EqualTo (3));
        }

        [Test]
        public void Bind_Call_ShouldLogSuccess ()
        {
            var info = string.Empty;
            using (var sut = new MDPBroker ("tcp://localhost:5555"))
            {
                sut.LogInfoReady += (s, e) => { info = e.Info; };

                sut.Bind ();

                Assert.That (info, Is.EqualTo ("[MDP BROKER] MDP Broker/0.3 is active at tcp://localhost:5555"));
            }
        }

        [Test]
        public void Bind_NoEndpointSet_ShouldThrowException ()
        {
            using (var sut = new MDPBroker ("No_Valid_Endpoint"))
            {
                Assert.Throws<ApplicationException> (sut.Bind);
            }
        }

        [Test]
        public void Bind_ReBind_ShouldLogSuccess ()
        {
            using (var sut = new MDPBroker ("No_Valid_Endpoint"))
            {
                var info = string.Empty;

                sut.LogInfoReady += (s, e) => { info = e.Info; };

                sut.Bind ("tcp://localhost:5555");

                Assert.That (info, Is.EqualTo ("[MDP BROKER] MDP Broker/0.3 is active at tcp://localhost:5555"));
            }
        }

        [Test]
        public void Bind_ReBindWhileBrokerIsRunning_ShouldThrowInvalidOperationException ()
        {
            using (var sut = new MDPBroker ("tcp://localhost:5556"))
            {
                var cts = new CancellationTokenSource ();

                sut.Run (cts.Token);

                Assert.Throws<InvalidOperationException> (() => sut.Bind ("tcp://localhost:5555"));

                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ReceiveREADYMessageFromWorker_LogSuccessfulRegistration ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            using (var cts = new CancellationTokenSource ())
            using (var workerSession = new MDPWorker (endPoint, "echo"))
            using (var broker = new MDPBroker (endPoint))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // start echo task
                Task.Run (() => EchoWorker.Run (workerSession), cts.Token);
                // wait for everything to happen
                await Task.Delay (500);
                // cancel the tasks
                cts.Cancel ();
                // check on the logging
                Assert.That (log.Count, Is.EqualTo (2));
                Assert.That (log[1], Is.StringContaining ("added to service echo"));
            }
        }

        [Test]
        public void RunSynchronous_ReceiveREADYMessageFromWorker_LogSuccessfulRegistration ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            using (var cts = new CancellationTokenSource ())
            using (var workerSession = new MDPWorker (endPoint, "echo"))
            using (var broker = new MDPBroker (endPoint))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // start broker session
                Task.Run (() => broker.RunSynchronous (cts.Token));
                // start echo task
                Task.Run (() => EchoWorker.Run (workerSession), cts.Token);
                // wait for everything to happen
                Thread.Sleep (500);
                // cancel the tasks
                cts.Cancel ();
                // check on the logging
                Assert.That (log.Count, Is.EqualTo (2));
                Assert.That (log[1], Is.StringContaining ("added to service echo"));
            }
        }

        [Test]
        public async void Run_ReceiveREADYMessageFromThreeWorkersSameServices_LogSuccessfulRegistration ()
        {
            const string endPoint = "tcp://localhost:5556";
            var log = new List<string> ();
            var id01 = Encoding.ASCII.GetBytes ("W01");
            var id02 = Encoding.ASCII.GetBytes ("W02");
            var id03 = Encoding.ASCII.GetBytes ("W03");

            using (var cts = new CancellationTokenSource ())
            using (var worker1 = new MDPWorker (endPoint, "echo", id01))
            using (var worker2 = new MDPWorker (endPoint, "echo", id02))
            using (var worker3 = new MDPWorker (endPoint, "echo", id03))
            using (var broker = new MDPBroker (endPoint))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // start echo task
                Task.Run (() => EchoWorker.Run (worker1), cts.Token);
                Task.Run (() => EchoWorker.Run (worker2), cts.Token);
                Task.Run (() => EchoWorker.Run (worker3), cts.Token);
                // wait for everything to happen
                await Task.Delay (1000);
                // cancel the tasks
                cts.Cancel ();
                // check on the logging
                Assert.That (log.Count, Is.EqualTo (4));
                Assert.That (log.Count (s => s.Contains ("READY processed")), Is.EqualTo (3));
                Assert.That (log.Count (s => s.Contains ("W01")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("W02")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("W03")), Is.EqualTo (1));
            }
        }

        [Test]
        public async void Run_ReceiveREADYMessageFromThreeWorkersDifferentServices_LogSuccessfulRegistration ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();
            var id01 = Encoding.ASCII.GetBytes ("Worker01");
            var id02 = Encoding.ASCII.GetBytes ("Worker02");
            var id03 = Encoding.ASCII.GetBytes ("Worker03");

            using (var cts = new CancellationTokenSource ())
            using (var worker1 = new MDPWorker (endPoint, "echo", id01))
            using (var worker2 = new MDPWorker (endPoint, "double echo", id02))
            using (var worker3 = new MDPWorker (endPoint, "add hello", id03))
            using (var broker = new MDPBroker (endPoint))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // start echo task
                Task.Run (() => EchoWorker.Run (worker1), cts.Token);
                Task.Run (() => DoubleEchoWorker (worker2), cts.Token);
                Task.Run (() => AddHelloWorker (worker3), cts.Token);
                // wait for everything to happen
                await Task.Delay (1000);
                // cancel the tasks
                cts.Cancel ();
                // check on the logging
                Assert.That (log.Count, Is.EqualTo (4));
                Assert.That (log.Count (s => s.Contains ("service echo")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("service double echo")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("service add hello")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("READY processed")), Is.EqualTo (3));
            }
        }

        [Test]
        public async void Run_ReceiveREPLYMessageFromWorker_ShouldLogCorrectReply ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();
            var debugLog = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var echoClient = new MDPClient (endPoint, idC01))
            using (var echoWorker = new MDPWorker (endPoint, "echo", idW01))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => EchoWorker.Run (echoWorker, longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // get the task for simulating the client
                var echoClientTask = new Task (() => EchoClient (echoClient, "echo"));
                // start and wait for completion of client
                echoClientTask.Start ();
                // the task completes when the message exchange is done
                await echoClientTask;
                // cancel the broker
                cts.Cancel ();

                Assert.That (log.Count, Is.EqualTo (2));
                Assert.That (log.Count (s => s.Contains ("Starting to listen for incoming messages")), Is.EqualTo (1));
                Assert.That (log.Count (s => s.Contains ("READY processed. Worker W1 added to service echo")), Is.EqualTo (1));

                if (debugLog.Count > 0)
                {
                    Assert.That (debugLog.Count, Is.EqualTo (16));
                    Assert.That (debugLog.Count (s => s.Contains ("Received")), Is.EqualTo (3));
                    Assert.That (debugLog.Contains ("[MDP BROKER DEBUG] Dispatching -> NetMQMessage[C1,,Hello World!] to echo"));
                    Assert.That (debugLog.Contains ("[MDP BROKER DEBUG] REPLY from W1 received and send to C1 -> NetMQMessage[MDPC01,echo,Hello World!]"));
                }
            }
        }

        [Test]
        public async void Run_ReceiveREPLYMessageFromThreeDifferentWorker_ShouldLogAndReturnCorrectReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();
            var debugLog = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idW02 = new[] { (byte) 'W', (byte) '2' };
            var idW03 = new[] { (byte) 'W', (byte) '3' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };
            var idC02 = new[] { (byte) 'C', (byte) '2' };
            var idC03 = new[] { (byte) 'C', (byte) '3' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var client01 = new MDPClient (endPoint, idC01))
            using (var client02 = new MDPClient (endPoint, idC02))
            using (var client03 = new MDPClient (endPoint, idC03))
            using (var worker01 = new MDPWorker (endPoint, "echo", idW01))
            using (var worker02 = new MDPWorker (endPoint, "double echo", idW02))
            using (var worker03 = new MDPWorker (endPoint, "add hello", idW03))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => EchoWorker.Run (worker01, longHeartbeatInterval), cts.Token);
                Task.Run (() => DoubleEchoWorker (worker02, longHeartbeatInterval), cts.Token);
                Task.Run (() => AddHelloWorker (worker03, longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // get the task for simulating the client
                var client01Task = new Task (() => EchoClient (client01, "echo"));
                var client02Task = new Task (() => DoubleEchoClient (client02, "double echo"));
                var client03Task = new Task (() => AddHelloClient (client03, "add hello"));
                // start and wait for completion of client
                client01Task.Start ();
                client02Task.Start ();
                client03Task.Start ();
                // the task completes when the message exchange is done
                Task.WaitAll (client01Task, client02Task, client03Task);
                // cancel the broker

                cts.Cancel ();

                Assert.That (log.Count, Is.EqualTo (4));
                Assert.That (log.Count (s => s.Contains ("READY")), Is.EqualTo (3));

                if (debugLog.Count > 0)
                {
                    Assert.That (debugLog.Count (s => s.Contains ("Received: NetMQMessage[W1")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("Received: NetMQMessage[W2")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("Received: NetMQMessage[W3")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("Dispatching -> NetMQMessage[C1")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("Dispatching -> NetMQMessage[C2")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("Dispatching -> NetMQMessage[C3")), Is.GreaterThanOrEqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("REPLY from W1")), Is.EqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("REPLY from W2")), Is.EqualTo (1));
                    Assert.That (debugLog.Count (s => s.Contains ("REPLY from W3")), Is.EqualTo (1));
                }
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsWithSingleWorker_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var client = new MDPClient (endPoint, idC01))
            using (var worker = new MDPWorker (endPoint, "echo", idW01))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // Create and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint, client));
                // cancel the broker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsWithMultipleWorker_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idW02 = new[] { (byte) 'W', (byte) '2' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var client = new MDPClient (endPoint, idC01))
            using (var worker1 = new MDPWorker (endPoint, "echo", idW01))
            using (var worker2 = new MDPWorker (endPoint, "echo", idW02))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker1, heartbeatinterval: longHeartbeatInterval), cts.Token);
                Task.Run (() => MultipleRequestWorker (worker2, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // Create and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint, client));
                // cancel the broker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsWithMultipleClients_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };
            var idC02 = new[] { (byte) 'C', (byte) '2' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var client1 = new MDPClient (endPoint, idC01))
            using (var client2 = new MDPClient (endPoint, idC02))
            using (var worker = new MDPWorker (endPoint, "echo", idW01))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // Create and run
                var c1 = Task.Run (() => MultipleRequestClient ("echo", endPoint, client1));
                var c2 = Task.Run (() => MultipleRequestClient ("echo", endPoint, client2));

                Task.WaitAll (c1, c2);
                // cancel the broker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsWithMultipleClientsAndMultipleWorker_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idW02 = new[] { (byte) 'W', (byte) '2' };
            var idC01 = new[] { (byte) 'C', (byte) '1' };
            var idC02 = new[] { (byte) 'C', (byte) '2' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var client1 = new MDPClient (endPoint, idC01))
            using (var client2 = new MDPClient (endPoint, idC02))
            using (var worker1 = new MDPWorker (endPoint, "echo", idW01))
            using (var worker2 = new MDPWorker (endPoint, "echo", idW02))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker1, heartbeatinterval: longHeartbeatInterval), cts.Token);
                Task.Run (() => MultipleRequestWorker (worker2, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // Create and run
                var c1 = Task.Run (() => MultipleRequestClient ("echo", endPoint, client1));
                var c2 = Task.Run (() => MultipleRequestClient ("echo", endPoint, client2));

                Task.WaitAll (c1, c2);
                // cancel the broker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsClientStopsAndReconnectsWithSingleWorker_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var worker = new MDPWorker (endPoint, "echo", idW01))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // create and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint));
                // recreate and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint));
                // cancel the broker & worker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsClientStopsAndReconnectsWithMultipleWorker_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();
            var debugLog = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };
            var idW02 = new[] { (byte) 'W', (byte) '2' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var worker1 = new MDPWorker (endPoint, "echo", idW01))
            using (var worker2 = new MDPWorker (endPoint, "echo", idW02))
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (worker1, heartbeatinterval: longHeartbeatInterval), cts.Token);
                Task.Run (() => MultipleRequestWorker (worker2, heartbeatinterval: longHeartbeatInterval), cts.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // create and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint));
                // recreate and run
                await Task.Run (() => MultipleRequestClient ("echo", endPoint));
                // cancel the broker & worker
                cts.Cancel ();
            }
        }

        [Test]
        public async void Run_ProcessMultipleRequestsWorkerStops_ShouldCorrectlyRouteReplies ()
        {
            const string endPoint = "tcp://localhost:5555";
            var log = new List<string> ();

            var idW01 = new[] { (byte) 'W', (byte) '1' };

            const int longHeartbeatInterval = 10000; // 10s heartbeat -> stay out of my testing for now

            using (var broker = new MDPBroker (endPoint, longHeartbeatInterval))
            using (var cts = new CancellationTokenSource ())
            using (var ctsWorker = new CancellationTokenSource ())
            {
                broker.Bind ();
                // collect all logging information from broker
                broker.LogInfoReady += (s, e) => log.Add (e.Info);
                // follow more details
                //broker.DebugInfoReady += (s, e) => debugLog.Add (e.Info);
                // start broker session
                broker.Run (cts.Token);
                // wait a little for broker to get started
                await Task.Delay (250);
                // get the task for simulating the worker & start it
                Task.Run (() => MultipleRequestWorker (null, endPoint, longHeartbeatInterval), ctsWorker.Token);
                // wait a little for worker to get started & registered
                await Task.Delay (250);
                // get the task for simulating the client
                var clientTask = new Task (() => MultipleRequestClient ("echo", endPoint));
                // start and wait for completion of client
                clientTask.Start ();
                // Cancel worker
                ctsWorker.Cancel ();
                // the task completes when the message exchange is done
                await clientTask;
                // cancel the broker
                cts.Cancel ();
            }
        }

        // ======================= HELPER =========================

        private static void EchoClient (IMDPClient client, string serviceName)
        {
            var request = new NetMQMessage ();
            // set the request data
            request.Push ("Hello World!");
            // send the request to the service
            var reply = client.Send (serviceName, request);

            Assert.That (reply, Is.Not.Null, "[ECHO CLIENT] REPLY was <null>");
            Assert.That (reply.FrameCount, Is.EqualTo (1));
            Assert.That (reply.First.ConvertToString (), Is.EqualTo ("Hello World!"));
        }

        private static void MultipleRequestWorker (IMDPWorker worker = null, string endpoint = null, int heartbeatinterval = 2500)
        {
            var idW01 = new[] { (byte) 'W', (byte) '1' };

            worker = worker ?? new MDPWorker (endpoint, "echo", idW01);
            worker.HeartbeatDelay = TimeSpan.FromMilliseconds (heartbeatinterval);

            NetMQMessage reply = null;

            while (true)
            {
                // send the reply and wait for a request
                var request = worker.Receive (reply);
                // was the worker interrupted
                Assert.That (request, Is.Not.Null);
                // echo the request and wait for next request which will not come
                // here the task will be canceled
                reply = worker.Receive (request);
            }
        }

        private static void MultipleRequestClient (string serviceName, string endpoint, IMDPClient client = null)
        {
            const int _NO_OF_RUNS = 100;

            var reply = new NetMQMessage[_NO_OF_RUNS];
            var idC01 = new[] { (byte) 'C', (byte) '1' };

            client = client ?? new MDPClient (endpoint, idC01);

            var request = new NetMQMessage ();
            // set the request data
            request.Push ("Hello World!");
            // send the request to the service
            for (int i = 0; i < _NO_OF_RUNS; i++)
                reply[i] = client.Send (serviceName, request);

            client.Dispose ();

            Assert.That (reply, Has.None.Null);
            Assert.That (reply.All (r => r.First.ConvertToString () == "Hello World!"));
        }

        private static void DoubleEchoWorker (IMDPWorker worker, int heartbeatinterval = 2500)
        {
            worker.HeartbeatDelay = TimeSpan.FromMilliseconds (heartbeatinterval);
            var request = worker.Receive (null);
            // was the worker interrupted
            Assert.That (request, Is.Not.Null);
            // double the content of the request
            var payload = request.Last.ConvertToString ();
            payload += " - " + payload;
            request.RemoveFrame (request.Last);
            request.Append (payload);
            // echo the request and wait for next request which will not come
            // here the task will be canceled
            var newrequest = worker.Receive (request);
        }

        private static void DoubleEchoClient (IMDPClient client, string serviceName)
        {
            const string _PAYLOAD = "Hello World";
            var request = new NetMQMessage ();
            // set the request data
            request.Push (_PAYLOAD);
            // send the request to the service
            var reply = client.Send (serviceName, request);

            Assert.That (reply, Is.Not.Null, "[DOUBLE ECHO CLIENT] REPLY was <null>");
            Assert.That (reply.FrameCount, Is.EqualTo (1));
            Assert.That (reply.First.ConvertToString (), Is.EqualTo (_PAYLOAD + " - " + _PAYLOAD));
        }

        private static void AddHelloWorker (IMDPWorker worker, int heartbeatinterval = 2500)
        {
            worker.HeartbeatDelay = TimeSpan.FromMilliseconds (heartbeatinterval);
            // send the reply and wait for a request
            var request = worker.Receive (null);
            // was the worker interrupted
            Assert.That (request, Is.Not.Null);
            // add the extra the content to request
            var payload = request.Last.ConvertToString ();
            payload += " - HELLO";
            request.RemoveFrame (request.Last);
            request.Append (payload);
            // echo the request and wait for next request which will not come
            // here the task will be canceled
            var newrequest = worker.Receive (request);
        }

        private static void AddHelloClient (IMDPClient client, string serviceName)
        {
            const string _PAYLOAD = "Hello World";
            var request = new NetMQMessage ();
            // set the request data
            request.Push (_PAYLOAD);
            // send the request to the service
            var reply = client.Send (serviceName, request);

            Assert.That (reply, Is.Not.Null, "[ADD HELLO CLIENT] REPLY was <null>");
            Assert.That (reply.FrameCount, Is.EqualTo (1));
            Assert.That (reply.First.ConvertToString (), Is.EqualTo (_PAYLOAD + " - HELLO"));
        }

        private static class EchoWorker
        {
            public static void Run (IMDPWorker worker, int heartbeatinterval = 2500)
            {
                worker.HeartbeatDelay = TimeSpan.FromMilliseconds (heartbeatinterval);
                // send the reply and wait for a request
                var request = worker.Receive (null);
                // was the worker interrupted
                Assert.That (request, Is.Not.Null);
                // echo the request and wait for next request which will not come
                // here the task will be canceled
                var newrequest = worker.Receive (request);
            }
        }
    }
}
