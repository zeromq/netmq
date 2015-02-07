using System;
using System.Text;

using NetMQ;
using NetMQ.Sockets;

using ParanoidPirate.Queue;

namespace ParanoidPirate.Client
{
    internal class Program
    {
        private static int sequence = 0;
        private static bool expectReply = true;
        private static int retriesLeft = Commons.REQUEST_CLIENT_RETRIES;

        /// <summary>
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
        private static void Main ()
        {
            using (var context = NetMQContext.Create ())
            {
                var client = CreateSocket (context);

                while (retriesLeft > 0)
                {
                    sequence++;

                    Console.WriteLine ("[Client] Sending ({0})", sequence);

                    client.Send (Encoding.Unicode.GetBytes (sequence.ToString ()));

                    expectReply = true;

                    while (expectReply)
                    {
                        var result = client.Poll (TimeSpan.FromMilliseconds (Commons.REQUEST_CLIENT_TIMEOUT));

                        if (!result)
                        {
                            retriesLeft--;

                            if (retriesLeft == 0)
                            {
                                Console.WriteLine ("[Client] Server seems to be offline, abandoning");
                                break;
                            }

                            Console.WriteLine ("[Client] No response from server, retrying...");

                            client.Disconnect (Commons.QUEUE_FRONTEND);
                            client.Close ();
                            client.Dispose ();

                            client = CreateSocket (context);
                            client.Send (Encoding.Unicode.GetBytes (sequence.ToString ()));
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     creates a REQ socket and connects it to the server
        ///     it also hooks up a ReceiveReady event handler and
        ///     sets the Linger timespan = 0
        /// </summary>
        /// <param name="context">the NetMQContext it operates under</param>
        /// <returns></returns>
        private static RequestSocket CreateSocket (NetMQContext context)
        {
            Console.WriteLine ("[Client] Connecting to server...");

            var client = context.CreateRequestSocket ();

            client.Connect (Commons.QUEUE_FRONTEND);

            client.Options.Linger = TimeSpan.Zero;

            client.ReceiveReady += OnReceiveReady;

            return client;
        }

        /// <summary>
        ///     the ReceiveReady event handler
        /// 
        ///     reads the incoming message and validates it against the sequence number
        /// </summary>
        private static void OnReceiveReady (object sender, NetMQSocketEventArgs arg)
        {
            var reply = arg.Socket.Receive ();
            var strReply = Encoding.Unicode.GetString (reply);

            if (Int32.Parse (strReply) == sequence)
            {
                Console.WriteLine ("[Client] Server replied OK ({0})", strReply);

                retriesLeft = Commons.REQUEST_CLIENT_RETRIES;
                expectReply = false;
            }
            else
                Console.WriteLine ("[Client] Malformed reply from server: {0}", strReply);
        }
    }
}
