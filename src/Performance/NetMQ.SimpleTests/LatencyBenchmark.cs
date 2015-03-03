using System;
using System.Diagnostics;
using System.Threading;

namespace NetMQ.SimpleTests
{
    internal class LatencyBenchmark : ITest
    {
        private const int Iterations = 20000;

        private static readonly int[] s_messageSizes = { 8, 64, 512, 4096, 8192, 16384, 32768 };

        public string TestName
        {
            get { return "Req/Rep Latency Benchmark"; }
        }

        public void RunTest()
        {
            Console.Out.WriteLine("Iterations: {0}", Iterations);
            Console.Out.WriteLine();
            Console.Out.WriteLine(" {0,-6} {1}", "Size", "Latency (µs)");
            Console.Out.WriteLine("---------------------");

            var client = new Thread(ClientThread) { Name = "Client" };
            var server = new Thread(ServerThread) { Name = "Server" };

            server.Start();
            client.Start();

            server.Join();
            client.Join();
        }

        private static void ClientThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreateRequestSocket())
            {
                socket.Connect("tcp://127.0.0.1:9000");

                foreach (int messageSize in s_messageSizes)
                {
                    var msg = new byte[messageSize];

                    var watch = Stopwatch.StartNew();

                    for (int i = 0; i < Iterations; i++)
                    {
                        socket.Send(msg);
                        socket.Receive(); // ignore response
                    }

                    watch.Stop();

                    const long tripCount = Iterations*2;
                    long ticks = watch.ElapsedTicks;
                    double seconds = (double)ticks/Stopwatch.Frequency;
                    double microsecond = seconds*1000000.0;
                    double microsecondsPerTrip = microsecond / tripCount;

                    Console.Out.WriteLine(" {0,-7} {1,6:0.0}", messageSize, microsecondsPerTrip);
                }
            }
        }

        private static void ServerThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreateResponseSocket())
            {
                socket.Bind("tcp://*:9000");

                for (int index = 0; index < s_messageSizes.Length; index++)
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        byte[] message = socket.Receive();
                        socket.Send(message);
                    }
                }
            }
        }
    }
}
