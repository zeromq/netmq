using System;

using NetMQ;

using MajordomoProtocol;

namespace MDPClientExample
{
    class Program
    {
        /// <summary>
        ///     usage:  MDPClientExample [-v]
        /// 
        ///     implements a MDPClient API usage
        /// </summary>
        static void Main (string[] args)
        {
            const string service_name = "echo";

            var verbose = args.Length > 1 && args[1] == "-v";
            int count;
            // create MDP client and set verboseness && use automatic disposal
            using (var session = new MDPClient ("tcp://localhost:5555"))
            {
                if (verbose)
                    session.LogInfoReady += (s, e) => Console.WriteLine ("{0}", e.LogInfo);

                for (count = 0; count < 100000; count++)
                {
                    var request = new NetMQMessage ();
                    // set the request data
                    request.Push ("Helo World!");
                    // send the request to the service
                    var reply = session.Send (service_name, request);

                    if (ReferenceEquals (reply, null))
                        break;
                }
            }

            Console.WriteLine ("{0} request/replies  processed!", count);
        }
    }
}
