using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace LazyPirate.Server2
{
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = "NetMQ LazyPirate Server 2";

            const string serverEndpoint = "tcp://127.0.0.1:5555";

            var random = new Random();

            using (var server = new ResponseSocket())
            {
                Console.WriteLine("S: Binding address {0}", serverEndpoint);
                server.Bind(serverEndpoint);

                var cycles = 0;

                while (true)
                {
                    var request = server.ReceiveFrameString();
                    cycles++;

                    if (cycles > 3 && random.Next(0, 10) == 0)
                    {
                        Console.WriteLine("S: Simulating a crash");
                        Thread.Sleep(5000);
                    }
                    else if (cycles > 3 && random.Next(0, 10) == 0)
                    {
                        Console.WriteLine("S: Simulating CPU overload");
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine("S: Normal request ({0})", request);
                    server.SendFrame(request);
                }
            }
        }
    }
}