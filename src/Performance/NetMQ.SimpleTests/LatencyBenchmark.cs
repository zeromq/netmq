namespace NetMQ.SimpleTests
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    internal class LatencyBenchmark : ITest
    {
        private const int RoundTripCount = 10000;

        private static readonly int[] MessageSizes = { 8, 64, 512, 4096, 8192, 16384, 32768 };

        public string TestName
        {
            get { return "Latency Benchmark"; }
        }

        public void RunTest()
        {
            var client = new Thread(ClientThread);
            var server = new Thread(ServerThread);

            client.Name = "Client";
            server.Name = "Server";

            server.Start();
            client.Start();

            server.Join(5000);
            client.Join(5000);
        }

        private static void ClientThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreateRequestSocket())
            {
                socket.Connect("tcp://127.0.0.1:9000");

                foreach (int messageSize in MessageSizes)
                {
                    var msg = new byte[messageSize];

                    var watch = new Stopwatch();
                    watch.Start();

                    for (int i = 0; i < RoundTripCount; i++)
                    {
                        socket.Send(msg);
                        socket.Receive(); // ignore response
                    }

                    watch.Stop();
                    long elapsedTime = watch.ElapsedTicks;

                    Console.WriteLine("Message size: " + messageSize + " [B]");
                    Console.WriteLine("Roundtrips: " + RoundTripCount);

                    double latency = (double)elapsedTime / RoundTripCount / 2 * 1000000 / Stopwatch.Frequency;
                    Console.WriteLine("Your average latency is {0} [us]", latency.ToString("f2"));
                }
            }
        }

        private static void ServerThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreateResponseSocket())
            {
                socket.Bind("tcp://*:9000");

                for (int index = 0; index < MessageSizes.Length; index++)
                {
                    for (int i = 0; i < RoundTripCount; i++)
                    {
                        byte[] message = socket.Receive();
                        socket.Send(message);
                    }
                }
            }
        }
    }
}
