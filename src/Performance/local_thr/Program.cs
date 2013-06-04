using System;
using System.Diagnostics;
using NetMQ.zmq;

namespace local_thr
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_thr <bind-to> <message-size> <message-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            var context = ZMQ.CtxNew();
            var pullSocket = ZMQ.Socket(context, ZmqSocketType.Pull);
            pullSocket.Bind(bindTo);

            var message = pullSocket.Recv(SendReceiveOptions.None);

            var stopWatch = Stopwatch.StartNew();
            for (int i = 0; i != messageCount - 1; i++)
            {
                message = pullSocket.Recv(SendReceiveOptions.None);
                if (message.Size != messageSize)
                {
                    Console.WriteLine("message of incorrect size received. Received: " + message.Size + " Expected: " + messageSize);
                    return -1;
                }
            }
            stopWatch.Stop();
            var millisecondsElapsed = stopWatch.ElapsedMilliseconds;
            if (millisecondsElapsed == 0)
                millisecondsElapsed = 1;

            message.Close();

            long messagesPerSecond = messageCount / millisecondsElapsed * 1000;
            double megabits = messagesPerSecond * messageSize * 8 / 1000000;

            Console.WriteLine("message size: {0} [B]", messageSize);
            Console.WriteLine("message count: {0}", messageCount);
            Console.WriteLine("mean throughput: {0} [msg/s]", messagesPerSecond);
            Console.WriteLine("mean throughput: {0:0.000} [Mb/s]", megabits);

            pullSocket.Close();
            context.Terminate();

            return 0;
        }
    }
}
