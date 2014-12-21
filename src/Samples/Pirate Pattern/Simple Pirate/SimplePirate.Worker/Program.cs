using System;
using System.Text;
using NetMQ;

namespace SimplePirate.Worker
{
    class Program
    {
        private const string LRU_READY = "READY";
        private const string SERVER_ENDPOINT = "tcp://localhost:5556";

        private static INetMQSocket worker;

        static void Main(string[] args)
        {
            using (var context = new Factory().CreateContext())
            {
                using (worker = context.CreateRequestSocket())
                {
                    var randomizer = new Random(DateTime.Now.Millisecond);
                    Guid guid = Guid.NewGuid();
                    worker.Options.Identity = Encoding.Unicode.GetBytes(guid.ToString());
                    worker.ReceiveReady += WorkerOnReceiveReady;
                    worker.Connect(SERVER_ENDPOINT);

                    Console.WriteLine("W: {0} worker ready", guid);
                    worker.Send(Encoding.Unicode.GetBytes(LRU_READY));

                    var cycles = 0;
                    while (true)
                    {
                        cycles += 1;
                        if (cycles > 3 && randomizer.Next(0, 5) == 0)
                        {
                            Console.WriteLine("W: {0} simulating a crash", guid);
                            System.Threading.Thread.Sleep(5000);
                        }
                        else if (cycles > 3 && randomizer.Next(0, 5) == 0)
                        {
                            Console.WriteLine("W: {0} simulating CPU overload", guid);
                            System.Threading.Thread.Sleep(3000);
                        }
                        Console.WriteLine("W: {0} normal reply", guid);

                        worker.Poll(TimeSpan.FromMilliseconds(1000));
                    }
                }
            }
        }

        private static void WorkerOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            //  Read and save all frames until we get an empty frame
            //  In this example there is only 1 but it could be more
            byte[] address = worker.Receive();
            byte[] empty = worker.Receive();
            byte[] request = worker.Receive();

            worker.SendMore(address);
            worker.SendMore(Encoding.Unicode.GetBytes(""));
            worker.Send(Encoding.Unicode.GetBytes(Encoding.Unicode.GetString(request) + " WORLD!"));
        }
    }
}
