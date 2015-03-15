using System;
using System.Diagnostics;
using System.Threading;
using MajordomoProtocol;
using NetMQ;

namespace MDPClientExample
{
    internal static class Program
    {
        /// <summary>
        ///     usage:  MDPClientExample [-v] [-rn] (1 ;lt n ;lt 100000 / Default == 10)
        /// 
        ///     implements a MDPClient API usage
        /// </summary>
        private static void Main(string[] args)
        {
            if (args.Length == 1 || args.Length > 2)
            {
                if (!(args[0] == "-v" || args[0].Contains("-r")))
                {
                    Console.WriteLine("MDPBrokerProcess [-v(erbose) OR -h(elp)]");
                    Console.WriteLine("\t-v => verbose");
                    Console.WriteLine("\tto stop processing use CTRL+C");

                    return;
                }
            }

            const string serviceName = "echo";
            const int maxRuns = 100000;

            bool verbose = false;
            int runs = 10;

            if (args.Length >= 1)
            {
                if (args.Length == 1 && args[0] == "-v")
                    verbose = true;
                else if (args.Length == 2)
                    verbose = args[0] == "-v" || args[1] == "-v";

<<<<<<< HEAD
            runs = runs == -1 ? 10 : runs > max_runs ? max_runs : runs;
=======
                if (args[0].Contains("-r") || args.Length > 1)
                {
                    if (args[0].Contains("-r"))
                        runs = GetInt(args[0]);
                    else if (args[1].Contains("-r"))
                        runs = GetInt(args[1]);
                }
            }

            runs = runs == -1 ? 10 : runs > maxRuns ? maxRuns : runs;
>>>>>>> remotes/upstream/master

            var id = new[] { (byte)'C', (byte)'1' };

            var watch = new Stopwatch();

            Console.WriteLine("Starting MDPClient and will send {0} requests.", runs);

            try
            {
                // create MDP client and set verboseness && use automatic disposal
                using (var session = new MDPClient("tcp://localhost:5555", id))
                {
                    if (verbose)
                        session.LogInfoReady += (s, e) => Console.WriteLine("{0}", e.Info);

                    // just give everything time to settle in
                    Thread.Sleep(500);

                    watch.Start();

<<<<<<< HEAD
                    int count;
                    for (count = 0; count < runs; count++)
=======
                    for (int count = 0; count < runs; count++)
>>>>>>> remotes/upstream/master
                    {
                        var request = new NetMQMessage();
                        // set the request data
                        request.Push("Helo World!");
                        // send the request to the service
                        var reply = session.Send(serviceName, request);

                        if (ReferenceEquals(reply, null))
                            break;
                    }

                    watch.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);

                return;
            }

            var time = watch.Elapsed;

            Console.WriteLine("{0} request/replies in {1} ms processed! Took {2} ms per REQ/REP",
                runs,
                time.TotalMilliseconds,
                time.TotalMilliseconds/runs);

            Console.Write("\nExit with any key!");
            Console.ReadKey();
        }

        private static int GetInt(string s)
        {
            var num = s.Remove(0, 2);

            int runs;
            var success = int.TryParse(num, out runs);

            return success ? runs : -1;
        }
    }
}
