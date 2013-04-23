using System;
using NetMQ;

namespace HelloWorld
{
    class Program
    {
        private static void Main(string[] args)
        {
            using (NetMQContext ctx = NetMQContext.Create())
            {
                using (var server = ctx.CreateResponseSocket())
                {
                    server.Bind("tcp://127.0.0.1:5556");

                    using (var client = ctx.CreateRequestSocket())
                    {
                        client.Connect("tcp://127.0.0.1:5556");

                        client.Send("Hello");

                        string m1 = server.ReceiveString();

                        Console.WriteLine("From Client: {0}", m1);

                        server.Send("Hi Back");

                        string m2 = client.ReceiveString();

                        Console.WriteLine("From Server: {0}", m2);

                        Console.ReadLine();
                    }
                }
            }
        }
    }
}
