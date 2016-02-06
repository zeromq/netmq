using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace InterBrokerRouter
{
    internal static class Program
    {
        public const byte WorkerReady = 0xFF; // worker ready message

        private const int NbrClients = 10;
        private const int NbrWorker = 5;

        // volatile to prevent the storage in a CPU register
        private static volatile bool s_keepRunning = true;

        /// <summary>
        ///     the broker setting up the cluster
        ///
        ///
        ///          State 2 ---+         +--- State n
        ///                     |         |
        ///                     +----+----+
        ///     client 1 ---|        |         |--- worker 1
        ///     client 2 ---+---- BROKER 1 ----+--- worker 2
        ///     :           |        |         |    :
        ///     client n ---+   +----+----+    +--- worker n
        ///                     |         |
        ///                  BROKER 2   BROKER n
        ///
        ///     BROKER 2 and n are not included and must be setup separately
        ///
        ///     A minimum of two address must be supplied
        /// </summary>
        /// <param name="args">[0] = this broker's address
        ///                    [1] = 1st peer's address
        ///                     :
        ///                    [n] = nth peer address</param>
        /// <remarks>
        ///     since "inproc://" is not working in NetMQ we use "tcp://"
        ///     for each broker we need 5 ports which for this example are
        ///     assigned as follows (in true life it should be configurable whether
        ///     they are ports or tcp/ip addresses)
        ///
        ///     this brokers address => local frontend binds to     tcp://127.0.0.1:5555
        ///                             cloud frontend binds to                    :5556
        ///                             local backend binds to                     :5557
        ///                             state backend binds to                     :5558
        ///                             monitor PULL binds to                      :5559
        ///
        ///     the sockets are connected as follows
        ///
        ///               this broker's monitor PUSH connects to    tcp://127.0.0.1:5559
        ///
        ///                         (if peer's address and port is  tcp://127.0.0.1:5575)
        ///
        ///               this broker's cloud backend connects to                  :5576
        ///               this broker's state frontend connects to                 :5578
        ///
        ///     this scheme is fix in this example
        /// </remarks>
        public static void Main(string[] args)
        {
            Console.Title = "NetMQ Inter-Broker Router";

            const string baseAddress = "tcp://127.0.0.1:";

            if (args.Length < 2)
            {
                Console.WriteLine("usage: program me peer1 [peer]*");
                Console.WriteLine("each broker needs 5 port for his sockets!");
                Console.WriteLine("place enough distance between multiple broker addresses!");
                Environment.Exit(-1);
            }

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                s_keepRunning = false;
            };

            // get random generator for later use
            var rnd = new Random();
            // get list for registering the clients
            var clients = new List<byte[]>(NbrClients);
            // get a list of peer addresses
            var peers = new List<byte[]>();
            // get all peer addresses - first is this broker!
            for (var i = 1; i < args.Length; i++)
                peers.Add(Encoding.UTF8.GetBytes(args[i]));

            // build this broker's address
            var me = baseAddress + args[0];
            // get the port as integer for later use
            var myPort = int.Parse(args[0]);

            Console.WriteLine("[BROKER] The broker can be stopped by using CTRL+C!");
            Console.WriteLine("[BROKER] setting up sockets ...");

            // set up all the addresses needed in the due course
            var localFrontendAddress = me;
            var cloudFrontendAddress = baseAddress + (myPort + 1);
            var localBackendAddress = baseAddress + (myPort + 2);
            var stateBackendAddress = baseAddress + (myPort + 3);
            var monitorAddress = baseAddress + (myPort + 4);

            // create the context and all the sockets
            using (var localFrontend = new RouterSocket())
            using (var localBackend = new RouterSocket())
            using (var cloudFrontend = new RouterSocket())
            using (var cloudBackend = new RouterSocket())
            using (var stateBackend = new PublisherSocket())
            using (var stateFrontend = new SubscriberSocket())
            using (var monitor = new PullSocket())
            {
                // give every socket an unique identity, e.g. LocalFrontend[Port]
                SetIdentities(myPort,
                    localFrontend,
                    cloudFrontend,
                    localBackend,
                    stateBackend,
                    monitor,
                    cloudBackend, stateFrontend);

                // subscribe to any message on the stateFrontend socket!
                stateFrontend.Subscribe("");

                // bind the serving sockets
                localFrontend.Bind(localFrontendAddress);
                cloudFrontend.Bind(cloudFrontendAddress);
                localBackend.Bind(localBackendAddress);
                stateBackend.Bind(stateBackendAddress);
                monitor.Bind(monitorAddress);

                // connect sockets to peers
                for (var i = 1; i < args.Length; i++)
                {
                    // build the cloud back end address
                    var peerPort = int.Parse(args[i]);
                    var address = baseAddress + (peerPort + 1);
                    Console.WriteLine("[BROKER] connect to cloud peer {0}", address);

                    // this cloudBackend connects to all peer cloudFrontends
                    cloudBackend.Connect(address);

                    // build the state front end address
                    address = baseAddress + (peerPort + 3);
                    Console.WriteLine("[BROKER] subscribe to state peer {0}", address);

                    // this stateFrontend to all peer stateBackends
                    stateFrontend.Connect(address);
                }

                // setup the local worker queue for LRU and monitor cloud capacity
                var workerQueue = new Queue<byte[]>();
                int previousLocalCapacity = 0;

                // receive the capacity available from other peer(s)
                stateFrontend.ReceiveReady += (s, e) =>
                {
                    // the message should contain the available cloud capacity
                    var capacity = e.Socket.ReceiveFrameString();

                    Debug.Assert(string.IsNullOrWhiteSpace(capacity), "StateFrontend: message was empty!");

                    int couldCapacity;
                    Debug.Assert(int.TryParse(capacity, out couldCapacity), "StateFrontend: message did not contain a number!");
                };

                // get the status message and print it
                monitor.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveFrameString();

                    Console.WriteLine("[MONITOR] {0}", msg);
                };

                // all local clients are connecting to this socket
                // they send a REQ and get a REPLY
                localFrontend.ReceiveReady += (s, e) =>
                {
                    // [client adr][empty][message id]
                    var request = e.Socket.ReceiveMultipartMessage();
                    // register the local client for later identification if not known
                    if (!clients.Any(n => AreSame(n, request[0])))
                        clients.Add(request[0].Buffer);
                    // if we have local capacity send worker else send to cloud
                    if (workerQueue.Count > 0)
                    {
                        // get the LRU worker adr
                        var worker = workerQueue.Dequeue();
                        // wrap message with workers address
                        var msg = Wrap(worker, request);
                        // send message to the worker
                        // [worker adr][empty][client adr][empty][data]
                        localBackend.SendMultipartMessage(msg);
                    }
                    else
                    {
                        // get an random index for peers
                        var peerIdx = rnd.Next(peers.Count - 2) + 2;
                        // get peers address
                        var peer = peers[peerIdx];
                        // wrap message with peer's address
                        var msg = Wrap(peer, request);
                        // [peer adr][empty][client adr][empty][data]
                        cloudBackend.SendMultipartMessage(msg);
                    }
                };

                // the workers are connected to this socket
                // we get a REPLY either for a cloud client [worker adr][empty][peer adr][empty][peer client adr][empty][data]
                // or local client [worker adr][empty][client adr][empty][data]
                // or a READY message [worker adr][empty][WORKER_READY]
                localBackend.ReceiveReady += (s, e) =>
                {
                    // a worker can send "READY" or a request
                    // or an REPLAY
                    var msg = e.Socket.ReceiveMultipartMessage();

                    // just to make sure we received a proper message
                    Debug.Assert(msg != null && msg.FrameCount > 0, "[LocalBackend] message was empty or frame count == 0!");

                    // get the workers identity
                    var id = Unwrap(msg);
                    // this worker done in either way so add it to available workers
                    workerQueue.Enqueue(id);
                    // if it is NOT a ready message we need to route the message
                    // it could be a reply to a peer or a local client
                    // [WORKER_READY] or [client adr][empty][data] or [peer adr][empty][peer client adr][empty][data]
                    if (msg[0].Buffer[0] != WorkerReady)
                    {
                        Debug.Assert(msg.FrameCount > 2, "[LocalBackend] None READY message malformed");

                        // if the adr (first frame) is any of the clients send the REPLY there
                        // and send it to the peer otherwise
                        if (clients.Any(n => AreSame(n, msg.First)))
                            localFrontend.SendMultipartMessage(msg);
                        else
                            cloudFrontend.SendMultipartMessage(msg);
                    }
                };

                // this socket is connected to all peers
                // we receive either a REQ or a REPLY form a peer
                // REQ [peer adr][empty][peer client adr][empty][message id] -> send to peer for processing
                // REP [peer adr][empty][client adr][empty][message id] -> send to local client
                cloudBackend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();

                    // just to make sure we received a message
                    Debug.Assert(msg != null && msg.FrameCount > 0, "[CloudBackend] message was empty or frame count == 0!");

                    // we need the peers address for proper addressing
                    var peerAdr = Unwrap(msg);

                    // the remaining message must be at least 3 frames!
                    Debug.Assert(msg.FrameCount > 2, "[CloudBackend] message malformed");

                    // if the id is any of the local clients it is a REPLY
                    // and a REQ otherwise
                    if (clients.Any(n => AreSame(n, msg.First)))
                    {
                        // [client adr][empty][message id]
                        localFrontend.SendMultipartMessage(msg);
                    }
                    else
                    {
                        // add the peers address to the request
                        var request = Wrap(peerAdr, msg);
                        // [peer adr][empty][peer client adr][empty][message id]
                        cloudFrontend.SendMultipartMessage(request);
                    }
                };

                // all peers are binding to this socket
                // we receive REPLY or REQ from peers
                // REQ [peer adr][empty][peer client adr][empty][data] -> send to local worker for processing
                // REP [peer adr][empty][client adr][empty][data] -> send to local client
                cloudFrontend.ReceiveReady += (s, e) =>
                {
                    var msg = e.Socket.ReceiveMultipartMessage();

                    // just to make sure we received a message
                    Debug.Assert(msg != null && msg.FrameCount > 0, "[CloudFrontend] message was empty or frame count == 0!");

                    // we may need the peers address for proper addressing
                    var peerAdr = Unwrap(msg);

                    // the remaining message must be at least 3 frames!
                    Debug.Assert(msg.FrameCount > 2, "[CloudFrontend] message malformed");

                    // if the address is any of the local clients it is a REPLY
                    // and a REQ otherwise
                    if (clients.Any(n => AreSame(n, msg.First)))
                        localFrontend.SendMultipartMessage(msg);
                    else
                    {
                        // in order to know which per to send back the peers adr must be added again
                        var original = Wrap(peerAdr, msg);

                        // reduce the capacity to reflect the use of a worker by a cloud request
                        previousLocalCapacity = workerQueue.Count;
                        // get the LRU worker
                        var workerAdr = workerQueue.Dequeue();
                        // wrap the message with the worker address and send
                        var request = Wrap(workerAdr, original);
                        localBackend.SendMultipartMessage(request);
                    }
                };

                // in order to reduce chatter we only check to see if we have local capacity to provide to cloud
                // periodically every 2 seconds with a timer
                var timer = new NetMQTimer((int)TimeSpan.FromSeconds(2).TotalMilliseconds);

                timer.Elapsed += (t, e) =>
                {
                    // send message only if the previous send information changed
                    if (previousLocalCapacity != workerQueue.Count)
                    {
                        // set the information
                        previousLocalCapacity = workerQueue.Count;
                        // generate the message
                        var msg = new NetMQMessage();
                        var data = new NetMQFrame(previousLocalCapacity.ToString());
                        msg.Append(data);
                        var stateMessage = Wrap(Encoding.UTF8.GetBytes(me), msg);
                        // publish info
                        stateBackend.SendMultipartMessage(stateMessage);
                    }

                    // restart the timer
                    e.Timer.Enable = true;
                };

                // start all clients and workers as threads
                var clientTasks = new Thread[NbrClients];
                var workerTasks = new Thread[NbrWorker];

                for (var i = 0; i < NbrClients; i++)
                {
                    var client = new Client(localFrontendAddress, monitorAddress, (byte)i);
                    clientTasks[i] = new Thread(client.Run) { Name = string.Format("Client_{0}", i) };
                    clientTasks[i].Start();
                }

                for (var i = 0; i < NbrWorker; i++)
                {
                    var worker = new Worker(localBackendAddress, (byte)i);
                    workerTasks[i] = new Thread(worker.Run) { Name = string.Format("Worker_{0}", i) };
                    workerTasks[i].Start();
                }

                // create poller and add sockets & timer
                var poller = new NetMQPoller
                {
                    localFrontend,
                    localBackend,
                    cloudFrontend,
                    cloudBackend,
                    stateFrontend,
                    stateBackend,
                    monitor,
                    timer
                };

                // start monitoring the sockets
                poller.RunAsync();

                // we wait for a CTRL+C to exit
                while (s_keepRunning)
                    Thread.Sleep(100);

                Console.WriteLine("Ctrl-C encountered! Exiting the program!");

                if (poller.IsRunning)
                    poller.Stop();

                poller.Dispose();
            }
        }

        /// <summary>
        ///     sets unique identities for all sockets
        /// </summary>
        private static void SetIdentities(
            int myPort,
            RouterSocket localFrontend,
            RouterSocket cloudFrontend,
            RouterSocket localBackend,
            PublisherSocket stateBackend,
            PullSocket monitor,
            RouterSocket cloudBackend,
            SubscriberSocket stateFrontend)
        {
            localFrontend.Options.Identity = Encoding.UTF8.GetBytes("LocalFrontend[" + myPort + "]");
            cloudFrontend.Options.Identity = Encoding.UTF8.GetBytes("CloudFrontend[" + (myPort + 1) + "]");
            localBackend.Options.Identity = Encoding.UTF8.GetBytes("LocalBackend[" + (myPort + 2) + "]");
            stateBackend.Options.Identity = Encoding.UTF8.GetBytes("StateBackend[" + (myPort + 3) + "]");
            monitor.Options.Identity = Encoding.UTF8.GetBytes("Monitor[" + (myPort + 4) + "]");
            cloudBackend.Options.Identity = Encoding.UTF8.GetBytes("CloudBackend");
            stateFrontend.Options.Identity = Encoding.UTF8.GetBytes("StateFrontend");
        }

        /// <summary>
        ///     check if the id and the id frame of the message are identical
        /// </summary>
        private static bool AreSame(byte[] id, NetMQFrame idFrame)
        {
            if (id.Length != idFrame.Buffer.Length)
                return false;

            return !id.Where((t, i) => t != idFrame.Buffer[i]).Any();
        }

        /// <summary>
        ///     get the identity of the sending socket
        ///     we hereby assume that the message has the following
        ///     sequence of frames and strips that off the message
        ///
        ///     [identity][empty][data]
        ///
        ///     whereby [data] cold be a wrapped message itself!
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private static byte[] Unwrap(NetMQMessage msg)
        {
            var idFrame = msg.Pop();
            // throw away the empty frame
            msg.Pop();

            return idFrame.Buffer;
        }

        /// <summary>
        ///     wraps the message with the identity and includes an empty frame
        /// </summary>
        /// <returns>[socket.Identity][empty][old message]</returns>
        private static NetMQMessage Wrap(byte[] identity, NetMQMessage msg)
        {
            var result = new NetMQMessage(msg);

            result.Push(NetMQFrame.Empty);
            result.Push(identity);

            return result;
        }

        /// <summary>
        ///     wraps the message with the identity of the socket and
        ///     includes an empty frame
        /// </summary>
        /// <returns>[socket.Identity][empty][old message]</returns>
        private static NetMQMessage Wrap(NetMQSocket socket, NetMQMessage msg)
        {
            return Wrap(socket.Options.Identity, msg);
        }
    }
}
