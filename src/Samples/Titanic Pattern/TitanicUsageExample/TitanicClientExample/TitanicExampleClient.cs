using System;
using System.Text;
using System.Threading;

using TitanicCommons;
using TitanicProtocol;

namespace TitanicClientExample
{
    /// <summary>
    ///     usage:  TitanicClientExample [-v] [-cN]
    /// 
    ///     implements a Titanic Client API usage
    /// 
    ///     -v  =>  verbose
    ///     -cN =>  repeat the request N times
    /// </summary>
    /// <remark>
    ///     this is a 'naked' approach and should not be used in real apps
    ///     the use or the client api should be extended for ease of use and
    ///      behavior in order to allow responsive apps
    /// </remark>
    internal class TitanicExampleClient
    {
        private static bool s_verbose;
        private static int s_runs = 1;

        private static void Main (string[] arguments)
        {
            const string address = "tcp://localhost:5555";
            //const string service_name = "echo";

            SetParameter (arguments);

            Console.WriteLine ("[TitanicClient] Staring Titanic Client\n");
            Console.WriteLine ("[TitanicClient] {0} / #{1} Messages\n\n", s_verbose ? "verbose" : "silent", s_runs);

            // wait to allow MDP/Titanic Broker to complete start up
            Thread.Sleep (500);

            using (ITitanicClient client = new TitanicClient (address))
            {
                if (s_verbose)
                    client.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

                for (var i = 0; i < s_runs; i++)
                {
                    Console.WriteLine ("[TitanicClient] Loop #{0} entered ...", i);

                    var data = "ERROR! NO REPLY";

                    // does 5 retries to get the reply
                    var reply = client.GetResult ("echo", "Hallo World", 5);

                    if (reply == null)
                    {
                        Console.WriteLine ("\t[TitanicClient] ran into a problem, received 'null' in loop #{0}", i);
                        continue;
                    }

                    data = reply.Item1 != null ? Encoding.UTF8.GetString (reply.Item1) : data;

                    if (data != "Hallo World")
                        Console.WriteLine ("\t[TitanicClient] Hallo World != {0} on loop #{1} with status {2}", data, i, reply.Item2);

                    if (!ReferenceEquals (reply.Item1, null) && reply.Item2 == TitanicReturnCode.Ok)
                        Console.WriteLine ("\t[TitanicClient] Status = {0} - Reply = {1}", reply.Item2, data);
                }
            }

            Console.WriteLine ("\n[TitanicClient] To exit press any key!");
            Console.ReadKey ();
        }

        private static void SetParameter (string[] arguments)
        {
            switch (arguments.Length)
            {
                case 0:
                    return;
                case 1:
                    if (arguments[0] == "-v")
                        s_verbose = true;

                    if (arguments[0].StartsWith ("-c"))
                        GetCount (arguments[0]);
                    break;
            }

            if (arguments.Length == 2)
            {
                if (arguments[0] == "-v" || arguments[1] == "-v")
                    s_verbose = true;

                if (arguments[0].StartsWith ("-c"))
                    GetCount (arguments[0]);
                else
                    if (arguments[1].StartsWith ("-c"))
                        GetCount (arguments[1]);
            }
        }

        private static void GetCount (string s)
        {
            var number = s.Replace (" ", "").Remove (0, 2);
            var n = int.Parse (number);
            s_runs = n <= 0 ? 1 : n;        // min. 1 run
        }
    }
}
