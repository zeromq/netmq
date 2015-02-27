using System;
using System.Text;
using System.Threading;
using NetMQ;

namespace LazyPirate.Server
{
    internal static class Program
    {
        private static void Main()
        {
            const string serverEndpoint = "tcp://127.0.0.1:5555";

            var random = new Random();

            using (var context = NetMQContext.Create())
            using (var server = context.CreateResponseSocket())
            {
                Console.WriteLine("S: Binding address {0}", serverEndpoint);
                server.Bind(serverEndpoint);

                var cycles = 0;

                while (true)
                {
                    byte[] request = server.Receive();
                    cycles++;

                    if (cycles > 3 && random.Next(0, 10) == 0)
                    {
                        Console.WriteLine("S: Simulating a crash");
                        Thread.Sleep(5000);
                    }
                    else if (cycles < 3 && random.Next(0, 10) == 0)
                    {
                        Console.WriteLine("S: Simulating CPU overload");
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine("S: Normal request ({0})", Encoding.Unicode.GetString(request));
                    server.Send(request);
                }
            }
        }
    }
}