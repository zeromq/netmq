using System;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace LazyPirate.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            const string SERVER_ENDPOINT = "tcp://127.0.0.1:5555";

            using (var context = new Factory().CreateContext())
            {
                var randomizer = new Random();

                using (var server = context.CreateResponseSocket())
                {
                    Console.WriteLine("S: Binding address {0}", SERVER_ENDPOINT);
                    server.Bind(SERVER_ENDPOINT);

                    var cycles = 0;

                    while (true)
                    {
                        byte[] request = server.Receive();
                        cycles++;

                        if (cycles > 3 && randomizer.Next(0, 10) == 0)
                        {
                            Console.WriteLine("S: Simulating a crash");
                            Thread.Sleep(5000);
                        }
                        else if (cycles < 3 && randomizer.Next(0, 10) == 0)
                        {
                            Console.WriteLine("S: Simulating CPU overload");
                            Thread.Sleep(1000);
                        }

                        Console.WriteLine("S: Normal request ({0})", Encoding.Unicode.GetString(request));
                        //Thread.Sleep(1000);
                        server.Send(request);
                    }
                }
            }
        }
    }
}
