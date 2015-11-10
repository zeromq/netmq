using System;
using System.Diagnostics;
using NetMQ;

namespace local_thr
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_thr <bind-to> <message-size> <message-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            using (var context = NetMQContext.Create())
            using (var pullSocket = context.CreatePullSocket())
            {
                pullSocket.Bind(bindTo);

                var msg = new Msg();
                msg.InitEmpty();

                pullSocket.Receive(ref msg);

                var stopWatch = Stopwatch.StartNew();
                for (int i = 0; i != messageCount - 1; i++)
                {
                    pullSocket.Receive(ref msg);
                    if (msg.Count != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + msg.Count + " Expected: " + messageSize);
                        return -1;
                    }
                }
                stopWatch.Stop();
                var millisecondsElapsed = stopWatch.ElapsedMilliseconds;
                if (millisecondsElapsed == 0)
                    millisecondsElapsed = 1;

                msg.Close();

                double messagesPerSecond = (double)messageCount/millisecondsElapsed*1000;
                double megabits = messagesPerSecond*messageSize*8/1000000;

                Console.WriteLine("message size: {0} [B]", messageSize);
                Console.WriteLine("message count: {0}", messageCount);
                Console.WriteLine("mean throughput: {0:0.000} [msg/s]", messagesPerSecond);
                Console.WriteLine("mean throughput: {0:0.000} [Mb/s]", megabits);
            }

            return 0;
        }
    }
}