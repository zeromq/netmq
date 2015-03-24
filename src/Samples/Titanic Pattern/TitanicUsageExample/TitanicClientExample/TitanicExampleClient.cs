using System;
using System.Threading;

using MajordomoProtocol;
using MajordomoProtocol.Contracts;

using NetMQ;

using TitanicCommons;

namespace TitanicClientExample
{
    /// <summary>
    ///     usage:  TitanicClientExample [-v]
    /// 
    ///     implements a Titanic Client API usage
    /// </summary>
    /// <remark>
    ///     this is a 'naked' approach and should not be used in real apps
    ///     the use or the client api should be extended for ease of use and
    ///      behavior in order to allow responsive apps
    /// </remark>
    class TitanicExampleClient
    {
        static void Main (string[] args)
        {
            const string address = "tcp://localhost:5555";

            var verbose = args.Length > 0 && args[0] == "-v";
            var exit = false;

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                exit = true;
            };

            Console.WriteLine ("Staring Titanic Client\n");

            // wait to allow MDP/Titanic Broker to complete start up
            Thread.Sleep (500);

            using (var client = new MDPClient (address))
            {
                if (verbose)
                    client.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

                // 1. send 'echo' request to Titanic
                var request = new NetMQMessage ();

                // set request data
                request.Push ("Hello World");       // [data]
                // set requested service
                request.Push ("echo");              // [service name][data]

                Console.WriteLine ("REQUEST service: {0}", request);

                var reply = ServiceCall (client, TitanicOperation.Request, request);

                if (!ReferenceEquals (reply, null) && !reply.IsEmpty)
                {
                    // [data] -> here is a GUID
                    var guid = reply.Pop ().ConvertToString ();

                    Console.WriteLine ("Titanic replied: GUID = {0}", guid);

                    // wait for a moment
                    Thread.Sleep (100);

                    // 2. wait for reply
                    while (!exit)
                    {
                        request = new NetMQMessage ();
                        request.Push (guid);

                        Console.WriteLine ("REQUEST reply for: {0}", request);

                        reply = ServiceCall (client, TitanicOperation.Reply, request);

                        if (!ReferenceEquals (reply, null) && !reply.IsEmpty)
                        {
                            var answer = reply.Last.ConvertToString ();

                            Console.WriteLine ("Titanic's answer to the reply request: {0}", answer);

                            // 3. close request
                            request = new NetMQMessage ();
                            request.Push (guid);

                            Console.WriteLine ("REQUEST close: {0}", request);

                            reply = ServiceCall (client, TitanicOperation.Close, request);

                            Console.WriteLine ("REQUEST closed.");

                            break;
                        }

                        Console.WriteLine ("INFO: No reply yet. Retrying ...");

                        Thread.Sleep (5000); // wait for 5s
                    }
                }
                else
                    Console.WriteLine ("ERROR: CORRUPTED REPLY BY TITANIC - PANIC!");
            }

            Console.WriteLine ("\nTo exit press any key!");
            Console.ReadKey ();
        }

        private static NetMQMessage ServiceCall (IMDPClient session, TitanicOperation op, NetMQMessage request)
        {
            // [operation][Guid]
            var reply = session.Send (op.ToString (), request);

            Console.WriteLine ("ServiceCall received: {0}", reply);

            if (ReferenceEquals (reply, null) || reply.IsEmpty)
                return null; // went wrong - why? I don't care!

            // we got a reply -> [operation][data]
            var rc = reply.Pop ().ConvertToString ();       // [operation] <- [data]
            var status = (MmiCodes) Enum.Parse (typeof (MmiCodes), rc);

            switch (status)
            {
                case MmiCodes.Ok:
                case MmiCodes.Pending:
                    return reply;   // [data]
                case MmiCodes.Unknown:
                    Console.WriteLine ("Service unknown - Abandoning");
                    return null;
                default:
                    Console.WriteLine ("ERROR: FATAL ERROR ABANDONING!");
                    return null;
            }
        }
    }
}
