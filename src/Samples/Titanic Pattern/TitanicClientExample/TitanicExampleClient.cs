using System;
using System.Threading;
using MajordomoProtocol;
using MajordomoProtocol.Contracts;
using NetMQ;
using TitanicCommons;
using TitanicProtocol;

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
            const string titanic_request = "titanic.request";
            const string titanic_reply = "titanic.reply";
            const string titanic_close = "titanic.close";

            var verbose = args.Length > 0 && args[0] == "-v";
            var exit = false;

            // trapping Ctrl+C as exit signal!
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                exit = true;
            };

            using (var client = new MDPClient (address))
            {
                if (verbose)
                    client.LogInfoReady += (s, e) => Console.WriteLine (e.Info);

                // 1. send 'echo' request to Titanic
                var request = new NetMQMessage ();
                // set requested service
                request.Push ("echo");
                // set request data
                request.Push ("Hello World");

                var reply = ServiceCall (client, titanic_request, request);

                if (!ReferenceEquals (reply, null) && !reply.IsEmpty)
                {
                    var guid = reply.Pop ().ConvertToString ();

                    Console.WriteLine ("Titanic replied: GUID = {0}", guid);

                    // wait for a moment
                    Thread.Sleep (100);

                    // 2. wait for reply
                    while (!exit)
                    {
                        request = new NetMQMessage ();
                        request.Push (guid);

                        reply = ServiceCall (client, titanic_reply, request);

                        if (!ReferenceEquals (reply, null) && !reply.IsEmpty)
                        {
                            var answer = reply.Last.ConvertToString ();

                            Console.WriteLine ("Reply: {0}", answer);

                            // 3. close request
                            request = new NetMQMessage ();
                            request.Push (guid);

                            reply = ServiceCall (client, titanic_close, request);

                            Console.WriteLine ("Closed request: {0}", reply);

                            break;
                        }
                        else
                        {
                            Console.WriteLine ("INFO: No reply yet. Retrying ...");

                            Thread.Sleep (5000); // wait for 5s
                        }
                    }
                }
                else
                    Console.WriteLine ("ERROR: CORRUPTED REPLY BY TITANIC - PANIC!");
            }
        }

        private static NetMQMessage ServiceCall (IMDPClient session, string service, NetMQMessage request)
        {
            var reply = session.Send (service, request);

            if (ReferenceEquals (reply, null) || reply.IsEmpty)
                return null; // went wrong - why? I don't care!

            // we got a reply
            var status = reply.Pop ().ConvertToString ();

            switch (status)
            {
                case "200":
                    return reply;
                case "400":
                case "500":
                default:
                    Console.WriteLine ("ERROR: FATAL ERROR ABANDONING!");
                    return null;
            }
        }
    }
}
