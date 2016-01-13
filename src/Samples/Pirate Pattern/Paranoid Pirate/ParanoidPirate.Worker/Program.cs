using System;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using ParanoidPirate.Queue;

namespace ParanoidPirate.Worker
{
    internal static class Program
    {
        /// <summary>
        /// ParanoidPirate.Worker [-v]
        ///
        /// The worker skeleton in the Paranoid Pirate Protocol
        ///
        /// It can detect if the queue died and vice versa by implementing hearbeating
        /// </summary>
        private static void Main(string[] args)
        {
            Console.Title = "NetMQ ParanoidPirate Worker";

            var verbose = args.Length > 0 && args[0] == "-v";

            var rnd = new Random();
            var workerId = rnd.Next(100, 500);
            var worker = GetWorkerSocket(verbose, workerId);

            // if liveliness == 0 -> queue is considered dead/disconnected
            var liveliness = Commons.HeartbeatLiveliness;
            var interval = Commons.IntervalInit;
            var heartbeatAt = DateTime.UtcNow.Millisecond + Commons.HeartbeatInterval;
            var cycles = 0;
            var crash = false;

            // upon receiving a message this event handler is called
            // read the message, randomly simulate failure/problem
            // process message or heartbeat or crash
            worker.ReceiveReady += (s, e) =>
            {
                var msg = e.Socket.ReceiveMultipartMessage();

                // message is a NetMQMessage (!)
                // - 3 part envelope + content -> request
                // - 1 part HEARTBEAT -> heartbeat
                if (msg.FrameCount > 3)
                {
                    // in order to test the robustness we simulate a couple of typical problems
                    // e.g. worker crushing or running very slow
                    // that is initiated after multiple cycles to give everything time to stabilize first
                    cycles++;

                    if (cycles > 3 && rnd.Next(5) == 0)
                    {
                        Console.WriteLine("[WORKER] simulating crashing!");
                        crash = true;
                        return;
                    }

                    if (cycles > 3 && rnd.Next(3) == 0)
                    {
                        Console.WriteLine("[WORKER] Simulating CPU overload!");
                        Thread.Sleep(500);
                    }

                    if (verbose)
                        Console.Write("[WORKER] working ...!");

                    // simulate high workload
                    Thread.Sleep(10);

                    if (verbose)
                        Console.WriteLine("[WORKER] sending {0}", msg.ToString());

                    // answer
                    e.Socket.SendMultipartMessage(msg);
                    // reset liveliness
                    liveliness = Commons.HeartbeatLiveliness;
                }
                else if (IsHeartbeatMessage(msg))
                    liveliness = Commons.HeartbeatLiveliness;
                else
                    Console.WriteLine("[WORKER] Received invalid message!");

                interval = Commons.IntervalInit;
            };

            while (!crash)
            {
                // wait for incoming request for specified milliseconds
                // if no message arrived it will return false and true otherwise
                // any arriving message fires the ReceiveReady event (!)
                if (!worker.Poll(TimeSpan.FromMilliseconds(Commons.HeartbeatInterval)))
                {
                    // the queue hasn't sent any messages for a while
                    // -> destroy socket and recreate and -connect
                    if (--liveliness == 0)
                    {
                        Console.WriteLine("\t[WORKER] heartbeat failure, can't reach the queue!");
                        Console.WriteLine("\t[WORKER] will reconnect in {0} ms ...", interval);

                        Thread.Sleep(interval);
                        // increase the interval each time we do not get any message in time
                        if (interval < Commons.IntervalMax)
                            interval *= 2;
                        else
                        {
                            Console.WriteLine("[WORKER - ERROR] something went wrong - abandoning");
                            crash = true;
                            break;
                        }

                        worker.Dispose();
                        worker = GetWorkerSocket(verbose, workerId);
                        liveliness = Commons.HeartbeatLiveliness;
                    }
                }
                // if it is time the worker will send a heartbeat so QUEUE can detect a dead worker
                if (DateTime.UtcNow.Millisecond > heartbeatAt)
                {
                    heartbeatAt = DateTime.UtcNow.Millisecond + Commons.HeartbeatInterval;

                    Console.WriteLine("[WORKER] sending heartbeat!");

                    worker.SendFrame(Commons.PPPHeartbeat);
                }
            }

            worker.Dispose();

            Console.Write("I crashed! To exit press any key!");
            Console.ReadKey();
        }

        /// <summary>
        /// Create the DEALER socket and connect it to QUEUE backend.
        /// Set the identity.
        /// Send the initial READY message.
        /// </summary>
        private static DealerSocket GetWorkerSocket(bool verbose, int id)
        {
            var worker = new DealerSocket { Options = { Identity = Encoding.UTF8.GetBytes("Worker_" + id) } };


            worker.Connect(Commons.QueueBackend);

            if (verbose)
                Console.WriteLine("[WORKER] {0} sending 'READY'.", Encoding.UTF8.GetString(worker.Options.Identity));

            // send READY
            worker.SendFrame(Commons.PPPReady);

            return worker;
        }

        /// <summary>
        /// Check if message is a HEARTBEAT.
        /// </summary>
        private static bool IsHeartbeatMessage(NetMQMessage msg)
        {
            return msg.FrameCount == 1 && msg.First.ConvertToString() == Commons.PPPHeartbeat;
        }
    }
}