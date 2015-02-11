using System;
using System.Text;
using System.Threading;

using NetMQ;
using NetMQ.Sockets;

using ParanoidPirate.Queue;

namespace ParanoidPirate.Worker
{
    class Program
    {
        /// <summary>
        ///     ParanoidPirate.Worker [-v]
        /// 
        ///     the worker skeleton in the Paranoid Pirate Protocol
        /// 
        ///     it can detect if the queue died and vice versa
        ///     by implementing hearbeating
        /// </summary>
        static void Main (string[] args)
        {
            var verbose = args.Length > 0 && args[0] == "-v";

            var rnd = new Random ();
            var workerId = rnd.Next (100, 500);

            using (var ctx = NetMQContext.Create ())
            {
                var worker = GetWorkerSocket (ctx, verbose, workerId);

                // if liveliness == 0 -> queue is considered dead/disconnected
                var liveliness = Commons.HEARTBEAT_LIVELINESS;
                var interval = Commons.INTERVAL_INIT;
                var heartbeatAt = DateTime.UtcNow.Millisecond + Commons.HEARTBEAT_INTERVAL;
                var cycles = 0;
                var crash = false;

                // upon receiving a message this event handler is called
                // read the message, randomly simulate failure/problem
                // process message or heartbeat or crash
                worker.ReceiveReady += (s, e) =>
                                       {
                                           var msg = e.Socket.ReceiveMessage ();

                                           // message is a NetMQMessage (!)
                                           // - 3 part envelope + content -> request
                                           // - 1 part HEARTBEAT -> heartbeat
                                           if (msg.FrameCount > 3)
                                           {
                                               // in order to test the robustness we simulate a couple of typical problems
                                               // e.g. worker crushing or running very slow
                                               // that is initiated after multiple cycles to give everything time to stabilize first
                                               cycles++;

                                               if (cycles > 3 && rnd.Next (5) == 0)
                                               {
                                                   Console.WriteLine ("[WORKER] simulating crashing!");
                                                   crash = true;
                                                   return;
                                               }

                                               if (cycles > 3 && rnd.Next (3) == 0)
                                               {
                                                   Console.WriteLine ("[WORKER] Simulating CPU overload!");
                                                   Thread.Sleep (500);
                                               }

                                               if (verbose)
                                                   Console.Write ("[WORKER] working ...!");

                                               // simulate high workload
                                               Thread.Sleep (10);

                                               if (verbose)
                                                   Console.WriteLine ("[WORKER] sending {0}", msg.ToString ());

                                               // answer
                                               e.Socket.SendMessage (msg);
                                               // reset liveliness
                                               liveliness = Commons.HEARTBEAT_LIVELINESS;
                                           }
                                           else
                                               if (MesageIsHeartbeat (msg))
                                                   liveliness = Commons.HEARTBEAT_LIVELINESS;
                                               else
                                                   Console.WriteLine ("[WORKER] Received invalid message!");

                                           interval = Commons.INTERVAL_INIT;
                                       };

                while (!crash)
                {
                    // wait for incoming request for specified milliseconds
                    // if no message arrived it will return false and true otherwise
                    // any arriving message fires the ReceiveReady event (!)
                    if (!worker.Poll (TimeSpan.FromMilliseconds (Commons.HEARTBEAT_INTERVAL)))
                    {
                        // the queue hasn't sent any messages for a while 
                        // -> destroy socket and recreate and -connect
                        if (--liveliness == 0)
                        {
                            Console.WriteLine ("\t[WORKER] heartbeat failure, can't reach the queue!");
                            Console.WriteLine ("\t[WORKER] will reconnect in {0} ms ...", interval);

                            Thread.Sleep (interval);
                            // increase the interval each time we do not get any message in time
                            if (interval < Commons.INTERVAL_MAX)
                                interval *= 2;
                            else
                            {
                                Console.WriteLine ("[WORKER - ERROR] something went wrong - abandoning");
                                crash = true;
                                break;
                            }

                            worker.Dispose ();
                            worker = GetWorkerSocket (ctx, verbose, workerId);
                            liveliness = Commons.HEARTBEAT_LIVELINESS;
                        }
                    }
                    // if it is time the worker will send a heartbeat so QUEUE can detect a dead worker
                    if (DateTime.UtcNow.Millisecond > heartbeatAt)
                    {
                        heartbeatAt = DateTime.UtcNow.Millisecond + Commons.HEARTBEAT_INTERVAL;

                        Console.WriteLine ("[WORKER] sending heartbeat!");

                        worker.Send (Commons.PPP_HEARTBEAT);
                    }
                }

                worker.Dispose ();
            }

            Console.Write ("I crashed! To exit press any key!");
            Console.ReadKey ();
        }

        /// <summary>
        ///     create the DEALER socket and connect it to QUEUE backend
        ///     set the identity
        /// send the initial READY message
        /// </summary>
        private static DealerSocket GetWorkerSocket (NetMQContext ctx, bool verbose, int id)
        {
            var worker = ctx.CreateDealerSocket ();

            worker.Options.Identity = Encoding.UTF8.GetBytes ("Worker_" + id);

            worker.Connect (Commons.QUEUE_BACKEND);

            if (verbose)
                Console.WriteLine ("[WORKER] {0} sending 'READY'.", Encoding.UTF8.GetString (worker.Options.Identity));

            // send READY
            worker.Send (Commons.PPP_READY);

            return worker;
        }

        /// <summary>
        ///     check if message is a HEARTBEAT
        /// </summary>
        private static bool MesageIsHeartbeat (NetMQMessage msg)
        {
            return msg.FrameCount == 1 && msg.First.ConvertToString () == Commons.PPP_HEARTBEAT;
        }
    }
}
