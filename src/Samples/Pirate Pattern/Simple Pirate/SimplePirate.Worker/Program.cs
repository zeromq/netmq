using System;
using System.Text;
using System.Threading;
using NetMQ;

namespace SimplePirate.Worker
{
    internal static class Program
    {
        private const string LRUReady = "READY";
        private const string ServerEndpoint = "tcp://localhost:5556";

        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var worker = context.CreateRequestSocket())
            {
                var random = new Random(DateTime.Now.Millisecond);
                var guid = Guid.NewGuid();

                worker.Options.Identity = Encoding.Unicode.GetBytes(guid.ToString());
                worker.Connect(ServerEndpoint);

                worker.ReceiveReady += (sender, socket) =>
                {
                    //  Read and save all frames until we get an empty frame
                    //  In this example there is only 1 but it could be more
                    byte[] address = worker.Receive();
                    worker.Receive(); // empty
                    byte[] request = worker.Receive();

                    worker.SendMore(address);
                    worker.SendMore(Encoding.Unicode.GetBytes(""));
                    worker.Send(Encoding.Unicode.GetBytes(Encoding.Unicode.GetString(request) + " WORLD!"));
                };

                Console.WriteLine("W: {0} worker ready", guid);
                worker.Send(Encoding.Unicode.GetBytes(LRUReady));

                var cycles = 0;
                while (true)
                {
                    cycles += 1;
                    if (cycles > 3 && random.Next(0, 5) == 0)
                    {
                        Console.WriteLine("W: {0} simulating a crash", guid);
                        Thread.Sleep(5000);
                    }
                    else if (cycles > 3 && random.Next(0, 5) == 0)
                    {
                        Console.WriteLine("W: {0} simulating CPU overload", guid);
                        Thread.Sleep(3000);
                    }
                    Console.WriteLine("W: {0} normal reply", guid);

                    worker.Poll(TimeSpan.FromMilliseconds(1000));
                }
            }
        }
    }
}