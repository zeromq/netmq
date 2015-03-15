using System;
using NetMQ;

namespace HelloWorld
{
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = "NetMQ HelloWorld";

            using (var context = NetMQContext.Create())
            using (var server = context.CreateResponseSocket())
            using (var client = context.CreateRequestSocket())
            {
                server.Bind("tcp://localhost:5556");
                client.Connect("tcp://localhost:5556");

                client.Send("Hello");

                Console.WriteLine("From Client: {0}", server.ReceiveFrameString());

                server.Send("Hi Back");

                Console.WriteLine("From Server: {0}", client.ReceiveFrameString());

                Console.WriteLine();
                Console.Write("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
