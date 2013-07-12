using System;
using System.Collections.Generic;
using NetMQ;

namespace Freelance.ModelOne.Client
{
    class Program
    {
        private const int REQUEST_TIMEOUT = 1000; // ms
        private const int MAX_RETRIES = 50;   // Before we abandon


        static void Main(string[] args)
        {
            List<string> endpoints = new List<string>();
            endpoints.Add("tcp://*** Add your first endpoint ***");
            endpoints.Add("tcp://*** Add your second endpoint ***");

            using (NetMQContext context = NetMQContext.Create())
            {
                if (endpoints.Count == 1)
                {
                    for (int i = 0; i < MAX_RETRIES; i++)
                    {
                        if (TryRequest(context, endpoints[0], string.Format("Hello from endpoint {0}", endpoints[0])))
                            break;
                    }
                }
                else if (endpoints.Count > 1)
                {
                    for (int i = 0; i < endpoints.Count; i++)
                    {
                        if (TryRequest(context, endpoints[i], string.Format("Hello from endpoint {0}", endpoints[i])))
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("No endpoints");
                }
            }

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();
        }

        private static bool TryRequest(NetMQContext context, string endpoint, string requestString)
        {
            Console.WriteLine("Trying echo service at {0}", endpoint);
            NetMQSocket client = context.CreateRequestSocket();
            client.Options.Linger = TimeSpan.Zero;
            client.Connect(endpoint);
            client.Send(requestString);
            client.ReceiveReady += ClientOnReceiveReady;
            bool pollResult = client.Poll(TimeSpan.FromMilliseconds(REQUEST_TIMEOUT));
            client.ReceiveReady -= ClientOnReceiveReady;
            client.Disconnect(endpoint);
            client.Dispose();

            return pollResult;
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            bool hasMore = true;

            var reply = e.Socket.ReceiveString(out hasMore);
            Console.WriteLine("Server replied ({0})", reply);
        }
    }
}
