using System;
using System.Threading;
using NetMQ;

namespace ExtendedRequestReply
{
    internal static class RequestReplyWorker
    {
        private const string WorkerEndpoint = "tcp://127.0.0.1:5560";

        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var worker = context.CreateResponseSocket())
            {
                worker.Connect(WorkerEndpoint);

                while (true)
                {
                    var msg = worker.ReceiveMultipartMessage();
                    Console.WriteLine("Processing Message {0}", msg.Last.ConvertToString());

                    Thread.Sleep(500);
                    
                    worker.Send(msg.Last.ConvertToString());
                }
            }
        }
    }
}