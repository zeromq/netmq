using System;
using System.Collections.Generic;
using NetMQ;

namespace Freelance.ModelOne.Client
{
    internal static class Program
    {
        private const int RequestTimeout = 1000; // ms
        private const int MaxRetries = 50; // Before we abandon

        private static void Main()
        {
            var endpoints = new List<string>
            {
                "tcp://*** Add your first endpoint ***", 
                "tcp://*** Add your second endpoint ***"
            };

            using (var context = NetMQContext.Create())
            {
                if (endpoints.Count == 1)
                {
                    for (int i = 0; i < MaxRetries; i++)
                    {
                        if (TryRequest(context, endpoints[0], string.Format("Hello from endpoint {0}", endpoints[0])))
                            break;
                    }
                }
                else if (endpoints.Count > 1)
                {
                    foreach (string endpoint in endpoints)
                    {
                        if (TryRequest(context, endpoint, string.Format("Hello from endpoint {0}", endpoint)))
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

            using (var client = context.CreateRequestSocket())
            {
                client.Options.Linger = TimeSpan.Zero;

                client.Connect(endpoint);
                
                client.Send(requestString);
                client.ReceiveReady += ClientOnReceiveReady;
                bool pollResult = client.Poll(TimeSpan.FromMilliseconds(RequestTimeout));
                client.ReceiveReady -= ClientOnReceiveReady;
                client.Disconnect(endpoint);
                return pollResult;
            }
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            Console.WriteLine("Server replied ({0})", e.Socket.ReceiveString());
        }
    }
}