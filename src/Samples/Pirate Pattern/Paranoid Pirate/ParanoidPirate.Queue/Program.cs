using System;
using System.Text;

using NetMQ;

namespace ParanoidPirate.Queue
{
    public class Program
    {
        /// <summary>
        ///     ParanoidPirate.Queue [-v]
        /// 
        ///     does load-balancing with heartbeating on worker tasks to detect
        ///     crashed, blocked or slow running worker tasks    
        /// </summary>
        static void Main (string[] args)
        {
            // serves as flag for exiting the program
            var exit = false;
            // catch CTRL+C as exit command
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          exit = true;
                                      };

            var verbose = args.Length > 0 && args[0] == "-v";

            using (var ctx = NetMQContext.Create ())
            using (var frontend = ctx.CreateRouterSocket ())
            using (var backend = ctx.CreateRouterSocket ())
            using (var poller = new Poller ())
            {
                frontend.Bind (Commons.QUEUE_FRONTEND);
                backend.Bind (Commons.QUEUE_BACKEND);

                var workers = new Workers ();

                // client sends to this socket
                frontend.ReceiveReady += (s, e) =>
                                         {
                                             // only process incoming client requests
                                             // if we have workers available handle client requests as long as we have workers
                                             // storage capability of the socket otherwise and pick up later
                                             if (workers.Available)
                                             {
                                                 // get all message frames!
                                                 var request = frontend.ReceiveMessage ();

                                                 if (verbose)
                                                     Console.WriteLine ("[QUEUE] received {0}", request);

                                                 // get next available worker
                                                 var worker = workers.Next ();
                                                 // wrap message with worker's address
                                                 var msg = Wrap (worker, request);

                                                 if (verbose)
                                                     Console.WriteLine ("[QUEUE -> WORKER] sending {0}", msg);

                                                 backend.SendMessage (msg);
                                             }
                                         };

                // worker sends to this socket
                backend.ReceiveReady += (s, e) =>
                                        {
                                            var msg = e.Socket.ReceiveMessage ();

                                            if (verbose)
                                                Console.WriteLine ("[QUEUE <- WORKER] received {0}", msg);

                                            // use workers identity for load-balancing
                                            var workerIdentity = Unwrap (msg);
                                            var worker = new Worker (workerIdentity);
                                            workers.Ready (worker);
                                            // just convenience
                                            var readableWorkerId = workerIdentity.ConvertToString ();

                                            if (msg.FrameCount == 1)
                                            {
                                                var data = msg[0].ConvertToString ();
                                                // the message is either READY or HEARTBEAT or corrupted 
                                                switch (data)
                                                {
                                                    case Commons.PPP_HEARTBEAT:
                                                        Console.WriteLine ("[QUEUE <- WORKER] Received a Heartbeat from {0}",
                                                                           readableWorkerId);
                                                        break;
                                                    case Commons.PPP_READY:
                                                        Console.WriteLine ("[QUEUE <- WORKER] Received a READY form {0}",
                                                                           readableWorkerId);
                                                        break;
                                                    default:
                                                        Console.WriteLine ("[QUEUE <- WORKER] ERROR received an invalid message!");
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                if (verbose)
                                                    Console.WriteLine ("[QUEUE -> CLIENT] sending {0}", msg);

                                                frontend.SendMessage (msg);
                                            }
                                        };

                var timer = new NetMQTimer (Commons.HEARTBEAT_INTERVAL);
                // every specified ms QUEUE shall send a heartbeat to all connected workers
                timer.Elapsed += (s, e) =>
                                 {
                                     // send heartbeat to every worker
                                     foreach (var worker in workers)
                                     {
                                         var heartbeat = new NetMQMessage ();

                                         heartbeat.Push (new NetMQFrame (Commons.PPP_HEARTBEAT));
                                         heartbeat.Push (worker.Identity);

                                         Console.WriteLine ("[QUEUE -> WORKER] sending heartbeat!");

                                         backend.SendMessage (heartbeat);
                                     }
                                     // restart timer
                                     e.Timer.Enable = true;
                                     // remove all dead or expired workers
                                     workers.Purge ();
                                 };

                if (verbose)
                    Console.WriteLine ("[QUEUE] Start listening!");

                poller.AddSocket (frontend);
                poller.AddSocket (backend);

                poller.PollTillCancelledNonBlocking ();

                // hit CRTL+C to stop the while loop
                while (!exit) { }

                // Cleanup
                poller.RemoveTimer (timer);
                // stop the poler task
                poller.CancelAndJoin ();
                // remove the sockets - Dispose is called automatically
                poller.RemoveSocket (frontend);
                poller.RemoveSocket (backend);
            }
        }

        /// <summary>
        ///     strips the first frame of a message and the following if it is empty
        /// </summary>
        /// <returns>the first frame and changes the message</returns>
        private static NetMQFrame Unwrap (NetMQMessage msg)
        {
            var id = msg.Pop ();
            // forget the empty frame
            if (msg.First.Equals (NetMQFrame.Empty))
                msg.Pop ();

            return id;
        }

        /// <summary>
        ///  wraps a message with the identity and an empty frame
        /// </summary>
        /// <returns>new created message</returns>
        private static NetMQMessage Wrap (NetMQFrame identity, NetMQMessage msg)
        {
            var result = new NetMQMessage (msg);

            result.PushEmptyFrame ();
            result.Push (identity);

            return result;
        }
    }
}
