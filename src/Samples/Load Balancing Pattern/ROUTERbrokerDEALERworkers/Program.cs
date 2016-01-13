using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

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

            using (var client = new RouterSocket())
            {
                client.Bind(string.Format("tcp://localhost:{0}", PortNumber));

                foreach (var thread in workers)
                {
                    thread.Start(PortNumber);
                }

                // Wait for threads to connect, since otherwise the messages we send won't be routable.
                Thread.Sleep(1000);

                for (int taskNumber = 0; taskNumber < 1000; taskNumber++)
                {
                    // Send two message parts, first the address…
                    client.SendMoreFrame(random.Next(3) > 0 ? Encoding.Unicode.GetBytes("A") : Encoding.Unicode.GetBytes("B"));

                    // And then the workload
                    client.SendFrame("This is the workload");
                }

                client.SendMoreFrame(Encoding.Unicode.GetBytes("A"));
                client.SendFrame("END");

                client.SendMoreFrame(Encoding.Unicode.GetBytes("B"));
                client.SendFrame("END");
            }

            Console.ReadKey();
        }

        private static void WorkerTaskA(object portNumber)
        {
            using (var worker = new DealerSocket())
            {
                worker.Options.Identity = Encoding.Unicode.GetBytes("A");
                worker.Connect(string.Format("tcp://localhost:{0}", portNumber));

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
            using (var worker = new DealerSocket())
            {
                worker.Options.Identity = Encoding.Unicode.GetBytes("B");
                worker.Connect(string.Format("tcp://localhost:{0}", portNumber));

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