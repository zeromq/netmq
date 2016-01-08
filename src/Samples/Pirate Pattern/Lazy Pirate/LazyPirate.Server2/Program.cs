using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace LazyPirate.Server2
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "NetMQ LazyPirate Server 2";

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
                    var request = server.ReceiveMultipartMessage();
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

                    var strReply = request.First.ConvertToString();
                    Console.WriteLine("S: Normal request ({0})", strReply);
                    server.SendMultipartMessage(request);
                }
            }
        }
    }
}
