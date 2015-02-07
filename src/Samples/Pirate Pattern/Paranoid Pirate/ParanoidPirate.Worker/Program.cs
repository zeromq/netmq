using System;
using System.Threading;

using NetMQ;
using NetMQ.Sockets;
using NetMQ.zmq;

using ParanoidPirate.Queue;

namespace ParanoidPirate.Worker
{
    class Program
    {
        /// <summary>
        ///     the worker skeleton in the Paranoid Pirate Protocol
        /// 
        ///     it can detect if the queue died and vice versa
        ///     by implementing hearbeating
        /// </summary>
        static void Main ()
        {
            var rnd = new Random ();
            using (var ctx = NetMQContext.Create ())
            {
                var worker = GetWorkerSocket (ctx);

                // if liveliness hits 0 queue is considered dead/disconnected
                var liveliness = Commons.HEARTBEAT_LIVELINESS;
                var interval = Commons.INTERVAL_INIT;
                var heartbeatAt = Clock.NowMs () + Commons.HEARTBEAT_INTERVAL;
                var cycles = 0;

                while (true)
                {
                    if (worker.Poll (TimeSpan.FromMilliseconds (Commons.HEARTBEAT_INTERVAL)))
                    {
                        // message is ready
                        // it is NetMQMessage (!)
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

                            Console.Write ("[WORKER] working ...!");
                            Thread.Sleep (10);
                            Console.WriteLine (" done - replying now!");

                            worker.SendMessage (msg);
                            liveliness = Commons.HEARTBEAT_LIVELINESS;
                        }
                        else
                            if (IsHeartbeat (msg))
                                liveliness = Commons.HEARTBEAT_LIVELINESS;
                            else
                                Console.WriteLine ("[WORKER] Received invalid HEARTBEAT!");

                        interval = Commons.INTERVAL_INIT;
                    }
                    else    // the queue hasn't sent any messages for a while -> destroy socket and recreate and -connect
                    {
                        if (--liveliness == 0)
                        {
                            Console.WriteLine ("\t[WORKER] heartbeat failure, can't reach the queue!");
                            Console.WriteLine ("\t[WORKER] will reconnect in {0} ms ...", interval);

                            Thread.Sleep (interval);

                            if (interval < Commons.INTERVAL_MAX)
                                interval *= 2;
                            else
                            {
                                Console.WriteLine ("ERROR: interrupted");
                                break;
                            }

                            worker.Dispose ();
                            worker = GetWorkerSocket (ctx);
                            liveliness = Commons.HEARTBEAT_LIVELINESS;
                        }
                    }

                    if (Clock.NowMs () > heartbeatAt)
                    {
                        heartbeatAt = Clock.NowMs () + Commons.HEARTBEAT_INTERVAL;

                        Console.WriteLine ("[WORKER] sending heartbeat!");

                        worker.Send (Commons.PPP_HEARTBEAT);
                    }
                }

                worker.Dispose ();
            }
        }

        private static DealerSocket GetWorkerSocket (NetMQContext ctx)
        {
            var worker = ctx.CreateDealerSocket ();

            worker.Options.Identity = Guid.NewGuid ().ToByteArray ();

            worker.Connect (Commons.QUEUE_BACKEND);

            Console.WriteLine ("[WORKER] worker sending 'READY'.");

            // send READY
            worker.Send (Commons.PPP_READY);

            return worker;
        }

        private static bool IsHeartbeat (NetMQMessage msg)
        {
            return msg.FrameCount == 1 && msg.First.ConvertToString () == Commons.PPP_HEARTBEAT;
        }
    }
}
