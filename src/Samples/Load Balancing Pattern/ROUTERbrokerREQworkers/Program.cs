using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace ROUTERbrokerREQworkers
{
    internal static class Program
    {
        private const int WorkersCount = 10;
        private const int PortNumber = 5555;

        public static void Main()
        {
            var workers = new List<Thread>(WorkersCount);

            using (var client = new RouterSocket())
            {
                string cnn = string.Format("tcp://localhost:{0}", PortNumber);
                client.Bind(cnn);
                Console.WriteLine("[B] Connect to {0}", cnn);

                for (int workerNumber = 0; workerNumber < WorkersCount; workerNumber++)
                {
                    workers.Add(new Thread(WorkerTask));
                    workers[workerNumber].Start(PortNumber);
                }

                for (int taskNumber = 0; taskNumber < WorkersCount*10; taskNumber++)
                {
                    // LRU worker is next waiting in queue
                    string address = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", address);
                    string empty = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", empty);
                    string ready = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", ready);

                    client.SendMoreFrame(address);
                    //Console.WriteLine("[B] Message sent: {0}", address);
                    client.SendMoreFrame("");
                    //Console.WriteLine("[B] Message sent: {0}", "");
                    client.SendFrame("This is the workload");
                    //Console.WriteLine("[B] Message sent: {0}", "This is the workload");
                }

                // Now ask mamas to shut down and report their results
                for (int taskNbr = 0; taskNbr < WorkersCount; taskNbr++)
                {
                    string address = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", address);
                    string empty = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", empty);
                    string ready = client.ReceiveFrameString();
                    //Console.WriteLine("[B] Message received: {0}", ready);

                    client.SendMoreFrame(address);
                    //Console.WriteLine("[B] Message sent: {0}", address);
                    client.SendMoreFrame("");
                    //Console.WriteLine("[B] Message sent: {0}", "");
                    client.SendFrame("END");
                    //Console.WriteLine("[B] Message sent: {0}", "END");
                }
            }

            Console.ReadLine();
        }

        private static void WorkerTask(object portNumber)
        {
            var random = new Random(DateTime.Now.Millisecond);

            using (var worker = new RequestSocket())
            {
                // We use a string identity for ease here
                string id = ZHelpers.SetID(worker, Encoding.Unicode);
                string cnn = string.Format("tcp://localhost:{0}", portNumber);
                worker.Connect(cnn);
                Console.WriteLine("[W] ID {0} connect to {1}", id, cnn);

                int total = 0;

                bool end = false;
                while (!end)
                {
                    // Tell the router we're ready for work
                    worker.SendFrame("Ready");
                    //Console.WriteLine("[W] Message sent: {0}", msg);

                    // Get workload from router, until finished
                    string workload = worker.ReceiveFrameString();
                    //Console.WriteLine("[W] Workload received: {0}", workload);

                    if (workload == "END")
                    {
                        end = true;
                    }
                    else
                    {
                        total++;

                        Thread.Sleep(random.Next(1, 1000)); //  Simulate 'work'
                    }
                }

                Console.WriteLine("ID ({0}) processed: {1} tasks", Encoding.Unicode.GetString(worker.Options.Identity), total);
            }
        }
    }

    public static class ZHelpers
    {
        public static string SetID(NetMQSocket client, Encoding unicode)
        {
            var str = Guid.NewGuid().ToString(); // client.GetHashCode().ToString();
            client.Options.Identity = unicode.GetBytes(str);
            return str;
        }
    }
}