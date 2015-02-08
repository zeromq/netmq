using System;
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

            using (var ctx = NetMQContext.Create ())
            {
                var worker = GetWorkerSocket (ctx, verbose);

                // if liveliness == 0 -> queue is considered dead/disconnected
                var liveliness = Commons.HEARTBEAT_LIVELINESS;
                var interval = Commons.INTERVAL_INIT;
                var heartbeatAt = DateTime.UtcNow.Millisecond + Commons.HEARTBEAT_INTERVAL;
                var cycles = 0;

                while (true)
                {
                    // wait for incoming request for specified milliseconds
                    if (worker.Poll (TimeSpan.FromMilliseconds (Commons.HEARTBEAT_INTERVAL)))
                    {
                        // message is ready
                        // it is a NetMQMessage (!)
                        // - 3 part envelope + content -> request
                        // - 1 part HEARTBEAT -> heartbeat
                        var msg = worker.ReceiveMessage ();

                        if (msg.FrameCount > 3)
                        {
                            // in order to test the robustness we simulate a couple of typical problems
                            // e.g. worker crushing or running very slow
                            // that is initiated after multiple cycles to give everything time to stabilize first
                            cycles++;

                            if (cycles > 3 && rnd.Next (5) == 0)
                            {
                                Console.WriteLine ("[WORKER] simulating crashing!");
                                break;
                            }

                            if (cycles > 3 && rnd.Next (3) == 0)
                            {
                                Console.WriteLine ("[WORKER] Simulating CPU overload!");
                                Thread.Sleep (500);
                            }

                            if (verbose)
                                Console.Write ("[WORKER] working ...!");

                            // simulate workload
                            Thread.Sleep (10);

                            if (verbose)
                                Console.WriteLine (" done - replying now!");

                            worker.SendMessage (msg);
                            // reset liveliness
                            liveliness = Commons.HEARTBEAT_LIVELINESS;
                        }
                        else
                            if (MesageIsHeartbeat (msg))
                                liveliness = Commons.HEARTBEAT_LIVELINESS;
                            else
                                Console.WriteLine ("[WORKER] Received invalid message!");

                        interval = Commons.INTERVAL_INIT;
                    }
                    else
                    {
                        // the queue hasn't sent any messages for a while -> destroy socket and recreate and -connect
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
                                break;
                            }

                            worker.Dispose ();
                            worker = GetWorkerSocket (ctx, verbose);
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
        }

        /// <summary>
        ///     create the DEALER socket and connect it to QUEUE backend
        ///     set the identity
        /// send the initial READY message
        /// </summary>
        private static DealerSocket GetWorkerSocket (NetMQContext ctx, bool verbose)
        {
            var worker = ctx.CreateDealerSocket ();

            worker.Options.Identity = Guid.NewGuid ().ToByteArray ();

            worker.Connect (Commons.QUEUE_BACKEND);

            if (verbose)
                Console.WriteLine ("[WORKER] worker sending 'READY'.");

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
