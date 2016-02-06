using System;
using System.Diagnostics;
using System.Threading;
using MajordomoProtocol;
using NetMQ;

namespace MDPClientExample
{
    internal static class MDPClientExampleProgram
    {
        /// <summary>
        ///     usage:  MDPClientExample [-v] [-rn] (1 ;lt n ;lt 100000 / Default == 10)
        /// 
        ///     implements a MDPClient API usage
        /// </summary>
        private static void Main (string[] args)
        {
            if (args.Length == 1 || args.Length > 2)
            {
                if (!(args[0] == "-v" || args[0].Contains ("-r")))
                {
                    Console.WriteLine ("MDPClientExample [-v(erbose) OR -h(elp)]");
                    Console.WriteLine ("\t-v => verbose");
                    Console.WriteLine ("\tto stop processing use CTRL+C");

                    return;
                }
            }

            const string service_name = "echo";
            const int max_runs = 100000;

            bool verbose = false;
            int runs = 10;

            if (args.Length >= 1)
            {
                if (args.Length == 1 && args[0] == "-v")
                    verbose = true;
                else if (args.Length == 2)
                    verbose = args[0] == "-v" || args[1] == "-v";

                if (args[0].Contains ("-r") || args.Length > 1)
                {
                    if (args[0].Contains ("-r"))
                        runs = GetInt (args[0]);
                    else if (args[1].Contains ("-r"))
                        runs = GetInt (args[1]);
                }
            }

            runs = runs == -1 ? 10 : runs > max_runs ? max_runs : runs;

            var id = new[] { (byte) 'C', (byte) '1' };

            var watch = new Stopwatch ();

            Console.WriteLine ("Starting MDPClient and will send {0} requests to service <{1}>.", runs, service_name);
            Console.WriteLine("(writes '.' for every 100 and '|' for every 1000 requests)\n");

            try
            {
                // create MDP client and set verboseness && use automatic disposal
                using (var session = new MDPClient ("tcp://localhost:5555", id))
                {
                    if (verbose)
                        session.LogInfoReady += (s, e) => Console.WriteLine ("{0}", e.Info);

                    // just give everything time to settle in
                    Thread.Sleep (500);

                    watch.Start ();

                    for (int count = 0; count < runs; count++)
                    {
                        var request = new NetMQMessage ();
                        // set the request data
                        request.Push ("Hello World!");
                        // send the request to the service
                        var reply = session.Send (service_name, request);

                        if (ReferenceEquals (reply, null))
                            break;

                        if (count % 1000 == 0)
                            Console.Write ("|");
                        else
                            if (count % 100 == 0)
                                Console.Write (".");
                    }

                    watch.Stop ();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine ("ERROR:");
                Console.WriteLine (ex.Message);
                Console.WriteLine (ex.StackTrace);

                return;
            }

            var time = watch.Elapsed;

            Console.WriteLine ("{0} request/replies in {1} ms processed! Took {2:N3} ms per REQ/REP",
                runs,
                time.TotalMilliseconds,
                time.TotalMilliseconds / runs);

            Console.Write ("\nExit with any key!");
            Console.ReadKey ();
        }

        private static int GetInt (string s)
        {
            var num = s.Remove (0, 2);

            int runs;
            var success = int.TryParse (num, out runs);

            return success ? runs : -1;
        }
    }
}
