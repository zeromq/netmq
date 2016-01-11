using System;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace LazyPirate.Client
{
    /// <summary>
    /// Run both LazyPirate.Server and LazyPirate.Client simultaneously to see the demonstration of this pattern.
    /// </summary>
    internal static class Program
    {
        private const int RequestTimeout = 2500;
        private const int RequestRetries = 10;
        private const string ServerEndpoint = "tcp://127.0.0.1:5555";

        private static int s_sequence;
        private static bool s_expectReply = true;
        private static int s_retriesLeft = RequestRetries;

        private static void Main()
        {
            Console.Title = "NetMQ LazyPirate Client";

            RequestSocket client = CreateServerSocket();

            while (s_retriesLeft > 0)
            {
                s_sequence++;
                Console.WriteLine("C: Sending ({0})", s_sequence);
                client.SendFrame(Encoding.Unicode.GetBytes(s_sequence.ToString()));
                s_expectReply = true;

                while (s_expectReply)
                {
                    bool result = client.Poll(TimeSpan.FromMilliseconds(RequestTimeout));

                    if (result)
                        continue;

                    s_retriesLeft--;

                    if (s_retriesLeft == 0)
                    {
                        Console.WriteLine("C: Server seems to be offline, abandoning");
                        break;
                    }

                    Console.WriteLine("C: No response from server, retrying...");

                    TerminateClient(client);

                    client = CreateServerSocket();
                    client.SendFrame(Encoding.Unicode.GetBytes(s_sequence.ToString()));
                }
            }

            TerminateClient(client);
        }

        private static void TerminateClient(NetMQSocket client)
        {
            client.Disconnect(ServerEndpoint);
            client.Close();
        }

        private static RequestSocket CreateServerSocket()
        {
            Console.WriteLine("C: Connecting to server...");

            var client = new RequestSocket();
            client.Connect(ServerEndpoint);
            client.Options.Linger = TimeSpan.Zero;
            client.ReceiveReady += ClientOnReceiveReady;

            return client;
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs args)
        {
            var reply = args.Socket.ReceiveFrameBytes();
            string strReply = Encoding.Unicode.GetString(reply);

            if (Int32.Parse(strReply) == s_sequence)
            {
                Console.WriteLine("C: Server replied OK ({0})", strReply);
                s_retriesLeft = RequestRetries;
                s_expectReply = false;
            }
            else
            {
                Console.WriteLine("C: Malformed reply from server: {0}", strReply);
            }
        }
    }
}