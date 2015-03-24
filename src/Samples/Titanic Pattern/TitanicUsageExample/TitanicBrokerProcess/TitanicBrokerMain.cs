using System;
using System.Threading;
using System.Threading.Tasks;

using TitanicProtocol;

namespace TitanicBrokerProcess
{
    class TitanicBrokerMain
    {
        static void Main (string[] args)
        {
            var verbose = args.Length > 0 && args[0] == "-v";

            var cts = new CancellationTokenSource ();

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          cts.Cancel ();
                                      };

            Console.WriteLine ("Starting Titanic Broker {0}", verbose ? "verbose" : "silent");

            var titanic = new TitanicBroker ();

            if (verbose)
                titanic.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

            Task.Factory.StartNew (() => titanic.Run (), cts.Token).Wait ();

            cts.Dispose ();
        }
    }
}
