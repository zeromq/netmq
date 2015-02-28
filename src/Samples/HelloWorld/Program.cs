using System;
using NetMQ;

namespace HelloWorld
{
    internal static class Program
    {
        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var server = context.CreateResponseSocket())
            using (var client = context.CreateRequestSocket())
            {
                server.Bind("tcp://127.0.0.1:5556");
                client.Connect("tcp://127.0.0.1:5556");

                client.Send("Hello");

                Console.WriteLine("From Client: {0}", 
                    server.ReceiveString());

                server.Send("Hi Back");

                Console.WriteLine("From Server: {0}", 
                    client.ReceiveString());

                        Console.WriteLine();
                        Console.Write("Press any key to exit...");
                        Console.ReadKey();
            }
        }
    }
}
