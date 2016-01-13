using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using System.Linq;

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

            using (var router = new RouterSocket())
            {
                router.Options.SendHighWatermark = 0;
                router.Bind("tcp://*:5555");

                var dealers = new List<DealerSocket>();
                var identities = new List<Msg>();
                var random = new Random();
                var identity = new byte[50];

                for (var i = 0; i < dealerCount; i++)
                {
                    random.NextBytes(identity);
                    var dealer = new DealerSocket
                    {
                        Options =
                        {
                            Identity = identity.Skip(10).ToArray(),
                            ReceiveHighWatermark = 0
                        }
                    };

                    dealer.Connect("tcp://localhost:5555");

                    dealers.Add(dealer);
                    var msg = new Msg();
                    msg.InitGC(identity, 10, identity.Length); // test offsets
                    identities.Add(msg);
                }

                Thread.Sleep(500);

                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(500);

                    var stopwatch = Stopwatch.StartNew();

                    for (var i = 0; i < messageCount; i++)
                    {
                        var msg = identities[i%identities.Count];
                        router.Send(ref msg, true);
                        var msg2 = new Msg();
                        msg2.InitPool(1);
                        msg2.Put((byte) 'E');
                        router.Send(ref msg2, false);
                    }

                    stopwatch.Stop();

                    Console.WriteLine("{0:N1} messages sent per second", messageCount/stopwatch.Elapsed.TotalSeconds);
                }

                foreach (var dealerSocket in dealers)
                    dealerSocket.Dispose();
            }
        }
    }
}
