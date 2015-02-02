using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using NetMQ;

namespace InterBrokerRouter
{
    class Program
    {
        public const byte WORKER_READY = 0xFF;

        private const int _NBR_CLIENTS = 10;
        private const int _NBR_WORKER = 5;

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
        ///     BROKER 2 & n are not included and must be setup separately
        /// 
        ///     A minimum of two address must be supplied
        /// </summary>
        /// <param name="args">[0] = this broker's address
        ///                    [1] = 1st peer's address
        ///                     :
        ///                    [n] = nth peer address</param>
        /// <remarks>
        ///     since "inproc://" is not working in NetMQ we use "tcp://"
        ///     for each broker we need 5 ports which for this exsample are
        ///     assigned as follows (in true life it should be configureable whether 
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
        ///                         (if peer's address & port is    tcp://127.0.0.1:5575)
        ///             
        ///               this broker's cloud backend connects to                  :5576
        ///               this broker's state frontend connects to                 :5578
        /// 
        ///     this scheme is fix in this exsample
        /// </remarks>
        public static void Main (string[] args)
        {
            const string base_address = "tcp://127.0.0.1:";
            const int cloud_backend_port = 1;
            const int state_frontend_port = 3;

            if (args.Length < 2)
            {
                Console.WriteLine ("usage: program me peer1 {peer}*");
                Console.WriteLine ("each broker needs 5 port for his sockets!");
                Console.WriteLine ("place enough distance between multiple broker addresses!");
                Environment.Exit (-1);
            }

            var me = base_address + args[0];
            var myPort = int.Parse (args[0]);

            Console.WriteLine ("[Broker]: setting up sockets ...");

            var localFrontendAddress = me;
            var cloudFrontendAddress = base_address + (myPort + 1);
            var localBackendAddress = base_address + (myPort + 2);
            var stateBackendAddress = base_address + (myPort + 3);
            var monitorAddress = base_address + (myPort + 4);

            using (var ctx = NetMQContext.Create ())
            using (var localFrontend = ctx.CreateRouterSocket ())
            using (var cloudFrontend = ctx.CreateRouterSocket ())
            using (var localBackend = ctx.CreateRouterSocket ())
            using (var stateBackend = ctx.CreatePublisherSocket ())
            using (var monitor = ctx.CreatePushSocket ())
            using (var cloudBackend = ctx.CreateRouterSocket ())
            using (var stateFrontend = ctx.CreateSubscriberSocket ())
            {
                // give every socket an unique identity
                localFrontend.Options.Identity = Encoding.UTF8.GetBytes ("LocalFrontend[" + myPort + "]");
                cloudFrontend.Options.Identity = Encoding.UTF8.GetBytes ("CloudFrontend[" + (myPort + 1) + "]");
                localBackend.Options.Identity = Encoding.UTF8.GetBytes ("LocalBackend[" + (myPort + 2) + "]");
                stateBackend.Options.Identity = Encoding.UTF8.GetBytes ("StateBackend[" + (myPort + 3) + "]");
                monitor.Options.Identity = Encoding.UTF8.GetBytes ("Monitor[" + (myPort + 4) + "]");
                cloudBackend.Options.Identity = Encoding.UTF8.GetBytes ("CloudBackend[" + myPort + "]");
                stateFrontend.Options.Identity = Encoding.UTF8.GetBytes ("StateFrontend[" + (myPort + 3) + "]");

                // subscribe to any message on the stateFrontend socket
                stateFrontend.Subscribe ("");

                // bind the serving sockets
                localFrontend.Bind (localFrontendAddress);
                cloudFrontend.Bind (cloudFrontendAddress);
                localBackend.Bind (localBackendAddress);
                stateBackend.Bind (stateBackendAddress);
                monitor.Bind (monitorAddress);

                // connect sockets to peers
                foreach (var peer in args)
                {
                    var peerPort = int.Parse (peer);
                    var address = base_address + (peerPort + cloud_backend_port);

                    Console.WriteLine ("[BROKER]: connect to cloud peer {0}", address);
                    // this cloudBackend to all peer cloudFrontends
                    cloudBackend.Connect (address);

                    address = base_address + (peerPort + state_frontend_port);
                    Console.WriteLine ("[BROKER]: subscribe to state peer {0}", address);
                    // this stateFrontend to all peer stateBackends
                    stateFrontend.Connect (address);
                }

                // setup the local worker queue for LRU and monitor cloud capacity
                var workerQueue = new Queue<byte[]> ();
                var couldCapacity = 0;

                stateFrontend.ReceiveReady += (s, e) =>
                                              {
                                                  // the message should contain the available cloud capacity
                                                  var capacity = stateFrontend.ReceiveString ();

                                                  Debug.Assert (string.IsNullOrWhiteSpace (capacity));
                                              };

                // setup all ReceiveReady handler for the sockets
                localBackend.ReceiveReady += (s, e) =>          // the workers are connected to this socket
                                              {
                                                  // a worker can send "READY" or a request
                                                  var msg = e.Socket.ReceiveMessage ();

                                                  // just to make sure we received a message
                                                  Debug.Assert (msg != null && msg.FrameCount > 0);

                                                  // get the workers identity
                                                  var id = Unwrap (msg);
                                                  // this worker done in either way so add it to available workers
                                                  workerQueue.Enqueue (id);
                                                  // if it is NOT a ready message we need to route the message
                                                  // it could a reply to a peer or a local client
                                                  if (msg[2].Buffer[0] != WORKER_READY)
                                                  {
                                                      // the remaining message must be at least 3 frames!
                                                      Debug.Assert (msg.FrameCount > 2);

                                                      // if the id is any of the local worker send there
                                                      // and send it to the cloud otherwise
                                                      if (workerQueue.Any (n => AreSame (n, msg[0])))
                                                          localFrontend.SendMessage (msg);
                                                      else
                                                          cloudFrontend.SendMessage (msg);
                                                  }
                                              };

                cloudBackend.ReceiveReady += (s, e) =>          // this socket is connected to all peers
                                             {
                                                 // a peer could send a reply or a request
                                                 var msg = e.Socket.ReceiveMessage ();

                                                 // just to make sure we received a message
                                                 Debug.Assert (msg != null && msg.FrameCount > 0);


                                             };

                // start all clients and workers as threads

            }
        }

        /// <summary>
        ///     check if the id and the id frame of the message are identical
        /// </summary>
        private static bool AreSame (byte[] id, NetMQFrame idFrame)
        {
            if (id.Length != idFrame.Buffer.Length)
                return false;

            return !id.Where ((t, i) => t != idFrame.Buffer[i]).Any ();
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
        static byte[] Unwrap (NetMQMessage msg)
        {
            var idFrame = msg.Pop ();
            // throw away the empty frame
            msg.Pop ();

            return idFrame.Buffer;
        }

        /// <summary>
        ///     wraps the message with the identity of the socket and
        ///     includes an empty frame
        /// </summary>
        /// <returns>[socket.Identity][empty][old message]</returns>
        static NetMQMessage Wrap (NetMQSocket socket, NetMQMessage msg)
        {
            var result = new NetMQMessage (msg);

            result.Push (NetMQFrame.Empty);
            result.Push (socket.Options.Identity);

            return result;
        }

    }
}
