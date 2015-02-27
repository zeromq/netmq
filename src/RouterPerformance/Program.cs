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
            const int count = 1000000;

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
                for (int i = 0; i < 100; i++)
                {
                    var dealer = context.CreateDealerSocket();

                    random.NextBytes(identity);

                    dealer.Options.Identity = identity;
                    dealer.Options.ReceiveHighWatermark = 0;
                    dealer.Connect("tcp://localhost:5555");

                    dealers.Add(dealer);
                    identities.Add(identity);
                }

                Thread.Sleep(1000);

                Stopwatch stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < count; i++)
                {
                    router.SendMore(identities[i%identities.Count]).Send("E");
                }

                stopwatch.Stop();

                Console.WriteLine("{0:N1} in second", count/stopwatch.Elapsed.TotalSeconds);
                Console.ReadLine();

                foreach (var dealerSocket in dealers)
                {
                    dealerSocket.Dispose();
                }
            }
        }
    }
}
