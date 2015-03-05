using System;
using System.Threading;
using System.Threading.Tasks;

using MajordomoProtocol;

namespace MDPBrokerProcess
{
    class Program
    {
        private static bool _verbose, _debug;

        private static void Main (string[] args)
        {
            if ((args.Length == 1 && args[0] != "-v") || args.Length > 1)
            {
                Console.WriteLine ("MDPBrokerProcess [-v(erbose) OR -h(elp)]");
                Console.WriteLine ("\t-v => verbose");
                Console.WriteLine ("\tto stop processing use CTRL+C");

                return;
            }

            Console.WriteLine ("MDP Broker - Majordomo Protocol V0.1\n");
            Console.WriteLine ("\tto stop processing use CTRL+C");

            _verbose = args.Length > 0 && args[0] == "-v";
            _debug = args.Length == 2 && args[1] == "-d";

            Console.WriteLine ("\nMessageLevel: Verbose = {0} and Debug = {1}", _verbose, _debug);

            // used to signal to stop the broker process
            var cts = new CancellationTokenSource ();

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          cts.Cancel ();
                                      };

            Console.WriteLine ("Starting Broker ...");

            try
            {
                RunBroker (cts).Wait ();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine ("ERROR:");
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine ("{0}", e.Message);
                    Console.WriteLine ("{0}", e.StackTrace);
                    Console.WriteLine ("---------------");
                }
                Console.WriteLine ("exit - any key");
                Console.ReadKey ();
            }
            catch (Exception ex)
            {
                Console.WriteLine ("ERROR:");
                Console.WriteLine (ex.Message);
                Console.WriteLine (ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine ("---------------");
                    Console.WriteLine (ex.InnerException.Message);
                    Console.WriteLine (ex.InnerException.StackTrace);
                    Console.WriteLine ("---------------");
                }
                Console.WriteLine ("exit - any key");
                Console.ReadKey ();
            }

        }
        
        private static async Task RunBroker (CancellationTokenSource cts)
        {
            using (var broker = new MDPBroker ("tcp://*:5555"))
            {
                if (_verbose)
                    broker.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

                if (_verbose)
                    broker.DebugInfoReady += (s, e) => Console.WriteLine (e.Info);

                await broker.Run (cts.Token);
            }
        }
    }
}
