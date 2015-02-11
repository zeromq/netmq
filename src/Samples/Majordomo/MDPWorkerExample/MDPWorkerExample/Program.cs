using System;

using MajordomoProtocol;

using NetMQ;

namespace MDPWorkerExample
{
    class Program
    {
        /// <summary>
        ///     usage:  MDPWorkerExample [-v]
        /// 
        ///     implements a MDPWorker API usage
        /// </summary>
        static void Main (string[] args)
        {
            var verbose = args.Length > 1 && args[1] == "-v";
            // create worker offering the service 'echo'
            using (var session = new MDPWorker ("tcp://localhost:5555", "echo"))
            {
                // logging info to be displayed on screen
                if (verbose)
                    session.LogInfoReady += (s, e) => Console.WriteLine ("{0}", e.LogInfo);

                // there is no inital reply
                NetMQMessage reply = null;

                while (true)
                {
                    // send the reply and wait for a request
                    var request = session.Receive (reply);
                    // was the worker interrupted
                    if (ReferenceEquals (request, null))
                        break;
                    // echo the request
                    reply = request;
                }
            }
        }
    }
}
