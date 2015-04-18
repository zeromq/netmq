using System;
using MajordomoProtocol;
using NetMQ;

namespace MDPWorkerExample
{
    internal static class MDPWorkerExampleProgram
    {
        /// <summary>
        /// Implements a MDPWorker API usage
        /// </summary>
        /// <remarks>
        /// Usage:  MDPWorkerExample [-v]
        /// </remarks>
        private static void Main (string[] args)
        {
            var verbose = args.Length == 1 && args[0] == "-v";
            var exit = false;
            const string service_name = "echo";

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          exit = true;
                                      };

            var id = new[] { (byte) 'W', (byte) '1' };

            Console.WriteLine ("Starting the MDP Worker offering service <{0}>", service_name);
            Console.WriteLine ("To exit CTRL-C!");

            try
            {
                // create worker offering the service 'echo'
                using (var session = new MDPWorker ("tcp://localhost:5555", service_name, id))
                {
                    session.HeartbeatDelay = TimeSpan.FromMilliseconds (10000);
                    // logging info to be displayed on screen
                    if (verbose)
                        session.LogInfoReady += (s, e) => Console.WriteLine ("{0}", e.Info);

                    // there is no initial reply
                    NetMQMessage reply = null;

                    while (!exit)
                    {
                        // send the reply and wait for a request
                        var request = session.Receive (reply);

                        if (verbose)
                            Console.WriteLine ("Received: {0}", request);

                        // was the worker interrupted
                        if (ReferenceEquals (request, null))
                            break;
                        // echo the request
                        reply = request;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine ("ERROR:");
                Console.WriteLine ("{0}", ex.Message);
                Console.WriteLine ("{0}", ex.StackTrace);

                Console.WriteLine ("exit - any key");
                Console.ReadKey ();
            }
        }
    }
}
