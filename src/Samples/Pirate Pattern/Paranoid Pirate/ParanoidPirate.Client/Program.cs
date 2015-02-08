using System;
using System.Runtime.InteropServices;
using System.Text;

using NetMQ;
using NetMQ.Sockets;

using ParanoidPirate.Queue;

namespace ParanoidPirate.Client
{
    internal class Program
    {
        private static int _sequence;
        private static bool _expectReply = true;
        private static int _retriesLeft = Commons.REQUEST_CLIENT_RETRIES;

        /// <summary>
        ///     ParanoidPirate.Client [-v]
        /// 
        ///     implements a skeleton client of a Paranoid Pirate Pattern
        /// 
        ///     upon start it creates a REQ socket 
        ///     send out a message with a sequence number
        ///     wait for a specified timespan for an answer
        ///     if the answer has not been received within that timeframe
        ///         write a message to screen
        ///         since the socket is corrupted disconnect and dispose
        ///         create a new REQ socket
        ///         resend the message
        ///     repeat that for a specified number of times
        /// </summary>
        private static void Main (string[] args)
        {
            var verbose = args.Length > 0 && args[0] == "-v";
            var clientId = args.Length > 1 ? args[1] : "SoleClient";

            using (var context = NetMQContext.Create ())
            {
                // create the REQ socket and connect to QUEUE frontend 
                // and hook up ReceiveReady event handler
                var client = CreateSocket (context, clientId);

                if (verbose)
                    Console.WriteLine ("[Client] Connected to Queue.");

                while (_retriesLeft > 0)
                {
                    _sequence++;

                    Console.WriteLine ("[Client] Sending ({0})", _sequence);

                    client.Send (Encoding.Unicode.GetBytes (_sequence.ToString ()));

                    _expectReply = true;

                    while (_expectReply)
                    {
                        if (client.Poll (TimeSpan.FromMilliseconds (Commons.REQUEST_CLIENT_TIMEOUT)))
                            continue;

                        // QUEUE has not answered in time
                        _retriesLeft--;

                        if (_retriesLeft == 0)
                        {
                            Console.WriteLine ("[Client - ERROR] Server seems to be offline, abandoning!");
                            break;
                        }

                        Console.WriteLine ("[Client - ERROR] No response from server, retrying...");

                        client.Disconnect (Commons.QUEUE_FRONTEND);
                        client.Close ();
                        client.Dispose ();

                        client = CreateSocket (context, clientId);
                        // resend sequence message
                        client.Send (Encoding.Unicode.GetBytes (_sequence.ToString ()));
                    }
                }

                // clean up!
                client.Disconnect (Commons.QUEUE_FRONTEND);
                client.Close ();
                client.Dispose ();
            }

            Console.Write ("I am done! To exits press any key!");
        }

        /// <summary>
        ///     just to create the REQ socket
        /// </summary>
        /// <param name="context">current NetMQContext</param>
        /// <param name="id">the name for the client</param>
        /// <returns>the connected REQ socket</returns>
        private static RequestSocket CreateSocket (NetMQContext context, string id)
        {
            var client = context.CreateRequestSocket ();

            client.Options.Identity = Encoding.UTF8.GetBytes (id);
            client.Options.Linger = TimeSpan.Zero;
            // set the event to be called upon arrival of a message
            client.ReceiveReady += OnClientReceiveReady;
            client.Connect (Commons.QUEUE_FRONTEND);

            return client;
        }

        /// <summary>
        ///     handles the ReceiveReady event
        ///     
        ///     get the message and validates that the send data is correct
        ///     prints an appropriate message on screen in either way
        /// </summary>
        private static void OnClientReceiveReady (object sender, NetMQSocketEventArgs e)
        {
            var reply = e.Socket.Receive ();
            var strReply = Encoding.Unicode.GetString (reply);

            if (Int32.Parse (strReply) == _sequence)
            {
                Console.WriteLine ("C: Server replied OK ({0})", strReply);

                _retriesLeft = Commons.REQUEST_CLIENT_RETRIES;
                _expectReply = false;
            }
            else
                Console.WriteLine ("C: Malformed reply from server: {0}", strReply);
        }
    }
}
