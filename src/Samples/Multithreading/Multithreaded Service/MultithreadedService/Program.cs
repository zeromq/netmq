using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Devices;
using NetMQ.Sockets;
using NetMQ;

namespace MultithreadedService
{
    class Program
    {
        private static CancellationToken token;

        static void Main(string[] args)
        {
            using (var context = NetMQContext.Create())
            {
                //var queue = new QueueDevice(context, "tcp://localhost:5555", "inproc://workers", DeviceMode.Threaded);
                var queue = new QueueDevice(context, "tcp://localhost:5555", "tcp://localhost:5556", DeviceMode.Threaded);

                CancellationTokenSource source = new CancellationTokenSource();
                token = source.Token;

                List<Task> workerThreads = new List<Task>();
                for (int threadId = 0; threadId < 10; threadId++)
                {
                    NetMQContext ctx = context;
                    workerThreads.Add(Task.Factory.StartNew(() => WorkerRoutine(new Worker(Guid.NewGuid(), ctx)), token));
                }

                queue.Start();

                List<Task> clientThreads = new List<Task>();
                for (int threadId = 0; threadId < 1000; threadId++)
                {
                    int id = threadId;
                    clientThreads.Add(Task.Factory.StartNew(() => ClientRoutine(id)));
                }

                Task.WaitAll(clientThreads.ToArray());

                source.Cancel();

                queue.Stop();
            }

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();
        }

        private static void ClientRoutine(object clientId)
        {
            try
            {
                using (NetMQContext context = NetMQContext.Create())
                {
                    using (RequestSocket req = context.CreateRequestSocket())
                    {
                        req.Connect("tcp://localhost:5555");

                        byte[] message = Encoding.Unicode.GetBytes(string.Format("{0} Hello", clientId));

                        Console.WriteLine("Client {0} sent \"{0} Hello\"", clientId);
                        req.Send(message, message.Length);

                        bool hasMore;
                        byte[] response = req.Receive(false, out hasMore);
                        Console.WriteLine("Client {0} received \"{1}\"", clientId, Encoding.Unicode.GetString(response));
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception on ClientRoutine: {0}", exc.Message);
            }
        }

        private static void WorkerRoutine(object workerContext)
        {
            try
            {
                Worker thisWorkerContext = (Worker)workerContext;

                using (ResponseSocket rep = thisWorkerContext.Context.CreateResponseSocket())
                {
                    rep.Options.Identity = Encoding.Unicode.GetBytes(Guid.NewGuid().ToString());
                    rep.Connect("tcp://localhost:5556");
                    //rep.Connect("inproc://workers");
                    rep.ReceiveReady += RepOnReceiveReady;
                    while (!token.IsCancellationRequested)
                    {
                        rep.Poll(TimeSpan.FromMilliseconds(100));
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception on WorkerRoutine: {0}", exc.Message);
                throw;
            }
        }

        private static void RepOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            try
            {
                NetMQSocket rep = socket.Socket;

                bool hasMore;
                byte[] message = rep.Receive(SendReceiveOptions.DontWait, out hasMore);

                //Thread.Sleep(1000); //  Simulate 'work'

                byte[] response =
                    Encoding.Unicode.GetBytes(Encoding.Unicode.GetString(message) + " World from worker " + Encoding.Unicode.GetString(rep.Options.Identity));

                rep.Send(response, response.Length, SendReceiveOptions.DontWait);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception on RepOnReceiveReady: {0}", exc.Message);
                throw;
            }
        }
    }

    public class Worker
    {
        public Guid WorkerId { get; private set; }
        public NetMQContext Context { get; private set; }

        public Worker(Guid workerId, NetMQContext context)
        {
            WorkerId = workerId;
            Context = context;
        }
    }
}
