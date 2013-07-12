using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace ROUTERbrokerDEALERworkers
{
    class Program
    {
        private const int PORT_NUMBER = 5555;

        //  We have two workers, here we copy the code, normally these would
        //  run on different boxes…
        public static void Main(string[] args)
        {
            var randomizer = new Random(DateTime.Now.Millisecond);
            var workers = new List<Thread>(new[] { new Thread(WorkerTaskA), new Thread(WorkerTaskB) });

            using (NetMQContext context = NetMQContext.Create())
            {
                using (RouterSocket client = context.CreateRouterSocket())
                {
                    client.Bind(string.Format("tcp://localhost:{0}", PORT_NUMBER));
                    foreach (Thread thread in workers)
                    {
                        thread.Start(PORT_NUMBER);
                    }

                    //  Wait for threads to connect, since otherwise the messages we send won't be routable.
                    Thread.Sleep(1000);

                    for (int taskNumber = 0; taskNumber < 1000; taskNumber++)
                    {
                        //  Send two message parts, first the address…

                        client.SendMore(randomizer.Next(3) > 0 ? Encoding.Unicode.GetBytes("A") : Encoding.Unicode.GetBytes("B"));

                        //  And then the workload
                        client.Send("This is the workload");
                    }

                    client.SendMore(Encoding.Unicode.GetBytes("A"));
                    client.Send("END");

                    client.SendMore(Encoding.Unicode.GetBytes("B"));
                    client.Send("END");
                }
            }

            Console.ReadKey();
        }

        private static void WorkerTaskA(object portNumber)
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (DealerSocket worker = context.CreateDealerSocket())
                {
                    worker.Options.Identity = Encoding.Unicode.GetBytes("A");
                    worker.Connect(string.Format("tcp://localhost:{0}", portNumber));

                    int total = 0;

                    bool end = false;

                    while (!end)
                    {
                        string request = worker.ReceiveString();

                        if (request.Equals("END"))
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
        }

        private static void WorkerTaskB(object portNumber)
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (DealerSocket worker = context.CreateDealerSocket())
                {
                    worker.Options.Identity = Encoding.Unicode.GetBytes("B");
                    worker.Connect(string.Format("tcp://localhost:{0}", portNumber));

                    int total = 0;

                    bool end = false;

                    while (!end)
                    {
                        string request = worker.ReceiveString();

                        if (request.Equals("END"))
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
}
