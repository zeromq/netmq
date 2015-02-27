using System;
using System.Text;
using NetMQ;
using NetMQ.Sockets;
using ParanoidPirate.Queue;

namespace ParanoidPirate.Client
{
    internal static class Program
    {
        private static int s_sequence;
        private static bool s_expectReply = true;
        private static int s_retriesLeft = Commons.RequestClientRetries;

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
        private static void Main(string[] args)
        {
            var verbose = args.Length > 0 && args[0] == "-v";
            var clientId = args.Length > 1 ? args[1] : "SoleClient";

            using (var context = NetMQContext.Create())
            {
                // create the REQ socket and connect to QUEUE frontend 
                // and hook up ReceiveReady event handler
                var client = CreateSocket(context, clientId);

                if (verbose)
                    Console.WriteLine("[Client] Connected to Queue.");

                while (s_retriesLeft > 0)
                {
                    s_sequence++;

                    Console.WriteLine("[Client] Sending ({0})", s_sequence);

                    client.Send(Encoding.Unicode.GetBytes(s_sequence.ToString()));

                    s_expectReply = true;

                    while (s_expectReply)
                    {
                        if (client.Poll(TimeSpan.FromMilliseconds(Commons.RequestClientTimeout)))
                            continue;

                        // QUEUE has not answered in time
                        s_retriesLeft--;

                        if (s_retriesLeft == 0)
                        {
                            Console.WriteLine("[Client - ERROR] Server seems to be offline, abandoning!");
                            break;
                        }

                        Console.WriteLine("[Client - ERROR] No response from server, retrying...");

                        client.Disconnect(Commons.QueueFrontend);
                        client.Close();
                        client.Dispose();

                        client = CreateSocket(context, clientId);
                        // resend sequence message
                        client.Send(Encoding.Unicode.GetBytes(s_sequence.ToString()));
                    }
                }

                // clean up!
                client.Disconnect(Commons.QueueFrontend);
                client.Dispose();
            }

            Console.Write("I am done! To exits press any key!");
        }

        /// <summary>
        ///     just to create the REQ socket
        /// </summary>
        /// <param name="context">current NetMQContext</param>
        /// <param name="id">the name for the client</param>
        /// <returns>the connected REQ socket</returns>
        private static RequestSocket CreateSocket(NetMQContext context, string id)
        {
            var client = context.CreateRequestSocket();

            client.Options.Identity = Encoding.UTF8.GetBytes(id);
            client.Options.Linger = TimeSpan.Zero;
            // set the event to be called upon arrival of a message
            client.ReceiveReady += OnClientReceiveReady;
            client.Connect(Commons.QueueFrontend);

            return client;
        }

        /// <summary>
        ///     handles the ReceiveReady event
        ///     
        ///     get the message and validates that the send data is correct
        ///     prints an appropriate message on screen in either way
        /// </summary>
        private static void OnClientReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            var reply = e.Socket.Receive();
            var strReply = Encoding.Unicode.GetString(reply);

            if (Int32.Parse(strReply) == s_sequence)
            {
                Console.WriteLine("C: Server replied OK ({0})", strReply);

                s_retriesLeft = Commons.RequestClientRetries;
                s_expectReply = false;
            }
            else
            {
                Console.WriteLine("C: Malformed reply from server: {0}", strReply);
            }
        }
    }
}