using System;
using System.Threading;
using NetMQ;

namespace ExtendedRequestReply
{
    class RequestReplyWorker
    {
        private const string WORKER_ENDPOINT = "tcp://127.0.0.1:5560";
        
        static void Main(string[] args)
        {
            using (var ctx = new Factory().CreateContext())
            {
                using (var worker = ctx.CreateResponseSocket())
                {
                    worker.Connect(WORKER_ENDPOINT);
                    while (true)
                    {
                        var msg = worker.ReceiveMessage();
                        Console.WriteLine("Processing Message {0}", msg.Last.ConvertToString());
                        Thread.Sleep(500);
                        worker.Send(msg.Last.ConvertToString());
                    }
                }
            }
        }
    }
}
