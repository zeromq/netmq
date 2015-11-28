using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NetMQ;

namespace ROUTERbrokerDEALERworkers
{
    internal static class Program
    {
        private const int PortNumber = 5555;

        // We have two workers, here we copy the code, normally these would run on different boxes…
        public static void Main()
        {
            var random = new Random(DateTime.Now.Millisecond);
            var workers = new List<Thread>(new[] { new Thread(WorkerTaskA), new Thread(WorkerTaskB) });

            using (var context = NetMQContext.Create())
            using (var client = context.CreateRouterSocket())
            {
                client.Bind($"tcp://localhost:{PortNumber}");

                foreach (var thread in workers)
                {
                    thread.Start(PortNumber);
                }

                // Wait for threads to connect, since otherwise the messages we send won't be routable.
                Thread.Sleep(1000);

                for (int taskNumber = 0; taskNumber < 1000; taskNumber++)
                {
                    // Send two message parts, first the address…
                    client.SendMore(random.Next(3) > 0 ? Encoding.Unicode.GetBytes("A") : Encoding.Unicode.GetBytes("B"));

                    // And then the workload
                    client.Send("This is the workload");
                }

                client.SendMore(Encoding.Unicode.GetBytes("A"));
                client.Send("END");

                client.SendMore(Encoding.Unicode.GetBytes("B"));
                client.Send("END");
            }

            Console.ReadKey();
        }

        private static void WorkerTaskA(object portNumber)
        {
            using (var context = NetMQContext.Create())
            using (var worker = context.CreateDealerSocket())
            {
                worker.Options.Identity = Encoding.Unicode.GetBytes("A");
                worker.Connect($"tcp://localhost:{portNumber}");

                int total = 0;

                bool end = false;

                while (!end)
                {
                    string request = worker.ReceiveFrameString();

                    if (request == "END")
                    {
                        end = true;
                    }
                    else
                    {
                        total++;
                    }
                }

                Console.WriteLine("A Received: {0}", total);
            }
        }

        private static void WorkerTaskB(object portNumber)
        {
            using (var context = NetMQContext.Create())
            using (var worker = context.CreateDealerSocket())
            {
                worker.Options.Identity = Encoding.Unicode.GetBytes("B");
                worker.Connect($"tcp://localhost:{portNumber}");

                int total = 0;

                bool end = false;

                while (!end)
                {
                    string request = worker.ReceiveFrameString();

                    if (request == "END")
                    {
                        end = true;
                    }
                    else
                    {
                        total++;
                    }
                }

                Console.WriteLine("B Received: {0}", total);
            }
        }
    }
}