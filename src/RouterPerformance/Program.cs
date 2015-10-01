using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace RouterPerformance
{
    internal static class Program
    {
        private static void Main()
        {
            const int messageCount = 1000000;
            const int dealerCount = 100;

            Console.WriteLine("Sending {0} messages to {1} dealers", messageCount, dealerCount);

            //BufferPool.SetBufferManagerBufferPool(1024 * 1024 * 10, 1024);

            using (var context = NetMQContext.Create())
            using (var router = context.CreateRouterSocket())
            {
                router.Options.SendHighWatermark = 0;
                router.Bind("tcp://*:5555");

                var dealers = new List<DealerSocket>();
                var identities = new List<byte[]>();
                var random = new Random();
                var identity = new byte[50];

                for (var i = 0; i < dealerCount; i++)
                {
                    var dealer = context.CreateDealerSocket();

                    random.NextBytes(identity);

                    dealer.Options.Identity = identity;
                    dealer.Options.ReceiveHighWatermark = 0;
                    dealer.Connect("tcp://localhost:5555");

                    dealers.Add(dealer);
                    identities.Add(identity);
                }

                Thread.Sleep(500);

                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(500);

                    var stopwatch = Stopwatch.StartNew();

                    for (var i = 0; i < messageCount; i++)
                        router.SendMoreFrame(identities[i%identities.Count]).SendFrame("E");

                    stopwatch.Stop();

                    Console.WriteLine("{0:N1} messages sent per second", messageCount/stopwatch.Elapsed.TotalSeconds);
                }

                foreach (var dealerSocket in dealers)
                    dealerSocket.Dispose();
            }
        }
    }
}
