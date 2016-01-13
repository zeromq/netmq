using System;
using NetMQ;
using NetMQ.Sockets;

namespace HelloWorld
{
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = "NetMQ HelloWorld";

            using (var server = new ResponseSocket("@tcp://localhost:5556"))
            using (var client = new RequestSocket("tcp://localhost:5556"))
            {
                client.SendFrame("Hello");

                Console.WriteLine("From Client: {0}", server.ReceiveFrameString());

                server.SendFrame("Hi Back");

                Console.WriteLine("From Server: {0}", client.ReceiveFrameString());

                Console.WriteLine();
                Console.Write("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
