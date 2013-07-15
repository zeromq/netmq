using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace ROUTERbrokerREQworkers
{
    class Program
    {
        private const int WORKERS_COUNT = 10;
        private const int PORT_NUMBER = 5555;

        public static void Main(string[] args)
        {
            var workers = new List<Thread>(WORKERS_COUNT);

            using (NetMQContext context = NetMQContext.Create())
            {
                using (RouterSocket client = context.CreateRouterSocket())
                {
                    string cnn = string.Format("tcp://localhost:{0}", PORT_NUMBER);
                    client.Bind(cnn);
                    Console.WriteLine("[B] Connect to {0}", cnn);

                    for (int workerNumber = 0; workerNumber < WORKERS_COUNT; workerNumber++)
                    {
                        workers.Add(new Thread(WorkerTask));
                        workers[workerNumber].Start(PORT_NUMBER);
                    }

                    for (int taskNumber = 0; taskNumber < WORKERS_COUNT * 10; taskNumber++)
                    {
                        //  LRU worker is next waiting in queue
                        string address = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", address);
                        string empty = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", empty);
                        string ready = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", ready);

                        client.SendMore(address);
                        //Console.WriteLine("[B] Message sent: {0}", address);
                        client.SendMore("");
                        //Console.WriteLine("[B] Message sent: {0}", "");
                        client.Send("This is the workload");
                        //Console.WriteLine("[B] Message sent: {0}", "This is the workload");
                    }

                    //  Now ask mamas to shut down and report their results
                    for (int taskNbr = 0; taskNbr < WORKERS_COUNT; taskNbr++)
                    {
                        string address = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", address);
                        string empty = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", empty);
                        string ready = client.ReceiveString();
                        //Console.WriteLine("[B] Message received: {0}", ready);

                        client.SendMore(address);
                        //Console.WriteLine("[B] Message sent: {0}", address);
                        client.SendMore("");
                        //Console.WriteLine("[B] Message sent: {0}", "");
                        client.Send("END");
                        //Console.WriteLine("[B] Message sent: {0}", "END");
                    }
                }
            }

            Console.ReadLine();
        }

        public static void WorkerTask(object portNumber)
        {
            var randomizer = new Random(DateTime.Now.Millisecond);

            using (NetMQContext context = NetMQContext.Create())
            {
                using (RequestSocket worker = context.CreateRequestSocket())
                {
                    //  We use a string identity for ease here
                    string id = ZHelpers.SetID(worker, Encoding.Unicode);
                    string cnn = string.Format("tcp://localhost:{0}", portNumber);
                    worker.Connect(cnn);
                    Console.WriteLine("[W] ID {0} connect to {1}", id, cnn);

                    int total = 0;

                    bool end = false;
                    while (!end)
                    {
                        //  Tell the router we're ready for work
                        string msg = "Ready";
                        worker.Send(msg);
                        //Console.WriteLine("[W] Message sent: {0}", msg);

                        //  Get workload from router, until finished
                        string workload = worker.ReceiveString();
                        //Console.WriteLine("[W] Workload received: {0}", workload);

                        if (workload.Equals("END"))
                        {
                            end = true;
                        }
                        else
                        {
                            total++;

                            Thread.Sleep(randomizer.Next(1, 1000)); //  Simulate 'work'
                        }
                    }

                    Console.WriteLine("ID ({0}) processed: {1} tasks", Encoding.Unicode.GetString(worker.Options.Identity), total);
                }
            }
        }
    }

    public class ZHelpers
    {
        public static string SetID(NetMQSocket client, Encoding unicode)
        {
            var str = Guid.NewGuid().ToString(); // client.GetHashCode().ToString();
            client.Options.Identity = unicode.GetBytes(str);
            return str;
        }
    }
}
