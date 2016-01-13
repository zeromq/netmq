using System;
using System.Diagnostics;
using NetMQ;
using NetMQ.Sockets;

namespace remote_lat
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_lat remote_lat <connect-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);

            using (var req = new RequestSocket())
            {
                req.Connect(connectTo);

                var msg = new Msg();
                msg.InitPool(messageSize);

                var stopWatch = Stopwatch.StartNew();

                for (int i = 0; i != roundtripCount; i++)
                {
                    req.Send(ref msg, more: false);

                    req.Receive(ref msg);

                    if (msg.Size != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: {0} Expected: {1}", msg.Size, messageSize);
                        return -1;
                    }
                }

                stopWatch.Stop();

                msg.Close();

                double elapsedMicroseconds = stopWatch.ElapsedTicks*1000000L/Stopwatch.Frequency;
                double latency = elapsedMicroseconds/(roundtripCount*2);

                Console.WriteLine("message size: {0} [B]", messageSize);
                Console.WriteLine("roundtrip count: {0}", roundtripCount);
                Console.WriteLine("average latency: {0:0.000} [µs]", latency);
            }

            return 0;
        }
    }
}
