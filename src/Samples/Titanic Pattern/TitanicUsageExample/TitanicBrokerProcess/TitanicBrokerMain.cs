using System;
using System.Threading;
using System.Threading.Tasks;

using TitanicProtocol;

namespace TitanicBrokerProcess
{
    internal class TitanicBrokerMain
    {
        private const ConsoleColor _broker = ConsoleColor.Cyan;
        private const ConsoleColor _request = ConsoleColor.Yellow;
        private const ConsoleColor _reply = ConsoleColor.White;
        private const ConsoleColor _close = ConsoleColor.Red;
        private const ConsoleColor _dispatch = ConsoleColor.Green;
        private const ConsoleColor _service_call = ConsoleColor.DarkGreen;

        private static void Main (string[] args)
        {
            var verbose = args.Length > 0 && args[0] == "-v";

            var cts = new CancellationTokenSource ();

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
                                      {
                                          e.Cancel = true;
                                          cts.Cancel ();
                                      };

            Console.WriteLine ("Starting Titanic Broker in {0} - mode.\n\n", verbose ? "verbose" : "silent");

            using (var titanic = new TitanicBroker (new TitanicMemoryIO ()))
            {
                if (verbose)
                    titanic.LogInfoReady += (s, e) => PrintMessage (e.Info);

                try
                {
                    Task.Factory.StartNew (() => titanic.Run (), cts.Token).Wait (cts.Token);
                }
                catch (AggregateException e)
                {
                    Console.WriteLine (e.Flatten ());
                }
                finally
                {
                    cts.Dispose ();
                }
            }
        }

        // print coloured according to message producer
        private static void PrintMessage (string msg)
        {
            var original = Console.ForegroundColor;

            if (msg.Contains ("BROKER"))
                Console.ForegroundColor = _broker;
            else
                if (msg.Contains ("REQUEST"))
                    Console.ForegroundColor = _request;
                else
                    if (msg.Contains ("REPLY"))
                        Console.ForegroundColor = _reply;
                    else
                        if (msg.Contains ("CLOSE"))
                            Console.ForegroundColor = _close;
                        else
                            if (msg.Contains ("DISPATCH"))
                                Console.ForegroundColor = _dispatch;
                            else
                                Console.ForegroundColor = _service_call;

            Console.WriteLine (msg);

            Console.ForegroundColor = original;
        }
    }
}
