using System;
using MajordomoProtocol;
using NetMQ;

namespace TitanicWorkerExample
{
    internal class TitanicWorker
    {
        /// <summary>
        ///     usage:  TitanicWorkerExample [-v]
        /// 
        ///     implements a MDPWorker API usage
        /// </summary>
        private static void Main (string[] args)
        {
            const string service_name = "echo";
            var verbose = args.Length == 1 && args[0] == "-v";
            var exit = false;

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                exit = true;
            };
            var id = new[] { (byte) 'W', (byte) '1' };

            Console.WriteLine ("Starting the Titanic Worker - offering service {0}", service_name);
            Console.WriteLine ("to exit - CTR-C\n");

            try
            {
                // create worker offering the service 'echo'
                using (var session = new MDPWorker ("tcp://localhost:5555", service_name, id))
                {
                    // there is no inital reply
                    NetMQMessage reply = null;

                    while (!exit)
                    {
                        // send the reply and wait for a request
                        var request = session.Receive (reply);

                        if (verbose)
                            Console.WriteLine ("[TITANIC WORKER] Received: {0}", request);

                        // was the worker interrupted
                        if (ReferenceEquals (request, null))
                            break;
                        // echo the request
                        reply = request;

                        if (verbose)
                            Console.WriteLine ("[TITANIC WORKER] Reply: {0}", request);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine ("[TITANIC WORKER] ERROR:");
                Console.WriteLine ("{0}", ex.Message);
                Console.WriteLine ("{0}", ex.StackTrace);

                Console.WriteLine ("exit - any key");
                Console.ReadKey ();
            }
        }
    }
}
