using System;
using System.Text;
using System.Threading;
using MajordomoProtocol;

using NetMQ;

namespace MDPServiceDiscoveryClientExample
{
    class Program
    {
        /// <summary>
        ///     usage:  MDPServiceDiscoveryClientExample [-v]
        /// 
        ///     implements a MDPClient API usage with Service Discovery
        /// </summary>
        static void Main (string[] args)
        {
            const string service_to_lookup = "echo";
            const string service_discovery = "mmi.service";

            var verbose = args.Length == 1 && args[0] == "-v";

            var id = Encoding.ASCII.GetBytes ("SDC01");

            // give WORKER & BROKER time to settle
            Thread.Sleep (250);

            using (var session = new MDPClient ("tcp://localhost:5555", id))
            {
                if (verbose)
                    session.LogInfoReady += (s, e) => Console.WriteLine ("{0}", e.Info);

                var request = new NetMQMessage ();
                // set the service name
                request.Push (service_to_lookup);
                // send the request to service discovery
                var reply = session.Send (service_discovery, request);

<<<<<<< HEAD
                if (!ReferenceEquals (reply, null) && !reply.IsEmpty)
=======
                if (reply != null && !reply.IsEmpty)
>>>>>>> remotes/upstream/master
                {
                    var answer = reply.First.ConvertToString ();

                    Console.WriteLine ("Lookup {0} service returned: {1}", service_to_lookup, answer);
                }
                else
                    Console.WriteLine ("ERROR: no response from broker, seems like broker is NOT running!");
            }

            Console.Write ("Exit with any key.");
            Console.ReadKey ();
        }
    }
}
