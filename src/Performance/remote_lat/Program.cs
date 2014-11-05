using System;
using System.Diagnostics;
using NetMQ;

namespace remote_lat
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_lat remote_lat <connect-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);

            var context = NetMQContext.Create();
            var reqSocket = context.CreateRequestSocket();

            reqSocket.Connect(connectTo);

            var message = new Msg();
            message.InitPool(messageSize);

            var stopWatch = Stopwatch.StartNew();

            for (int i = 0; i != roundtripCount; i++)
            {
                reqSocket.Send(ref message, SendReceiveOptions.None);

                reqSocket.Receive(ref message, SendReceiveOptions.None);
                if (message.Size != messageSize)
                {
                    Console.WriteLine("message of incorrect size received. Received: " + message.Size + " Expected: " + messageSize);
                    return -1;
                }
            }

            stopWatch.Stop();

            message.Close();

            double elapsedMicroseconds = stopWatch.ElapsedTicks * 1000000 / Stopwatch.Frequency;
            double latency = elapsedMicroseconds / (roundtripCount * 2);

            Console.WriteLine("message size: {0} [B]", messageSize);
            Console.WriteLine("roundtrip count: {0}", roundtripCount);
            Console.WriteLine("average latency: {0:0.000} [µs]", latency);

            reqSocket.Close();
            context.Terminate();

            return 0;
        }
    }
}
