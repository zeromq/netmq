using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace ExtendedRequestReply
{
    internal static class RequestReplyWorker
    {
        private const string WorkerEndpoint = "tcp://127.0.0.1:5560";

        private static void Main()
        {
            using (var worker = new ResponseSocket())
            {
                worker.Connect(WorkerEndpoint);

                while (true)
                {
                    var msg = worker.ReceiveMultipartMessage();
                    Console.WriteLine("Processing Message {0}", msg.Last.ConvertToString());

                    Thread.Sleep(500);

                    worker.SendFrame(msg.Last.ConvertToString());
                }
            }
        }
    }
}