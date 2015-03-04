using System;
using System.Diagnostics;
using System.Threading;

namespace NetMQ.SimpleTests
{
    internal class ThroughputBenchmark : ITest
    {
        private static readonly int[] s_messageSizes = { 8, 64, 256, 1024, 4096 };

        private const int MsgCount = 1000000;

        public string TestName
        {
            get { return "Throughput Benchmark"; }
        }

        public void RunTest()
        {
            Console.Out.WriteLine(" Messages: {0:#,##0}", MsgCount);
            Console.Out.WriteLine();
            Console.Out.WriteLine(" {0,-6} {1,10} {2,8}", "Size", "Msgs/sec", "Mb/s");
            Console.Out.WriteLine("----------------------------");

            var consumer = new Thread(ConsumerThread) { Name = "Consumer" };
            var producer = new Thread(ProducerThread) { Name = "Producer" };

            consumer.Start();
            producer.Start();

            producer.Join();
            consumer.Join();
        }

        private void ConsumerThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreatePullSocket())
            {
                socket.Bind("tcp://*:9091");

                foreach (var messageSize in s_messageSizes)
                {
                    var watch = Stopwatch.StartNew();

                    for (int i = 0; i < MsgCount; i++)
                    {
                        var message = socket.Receive();
                        Debug.Assert(message.Length == messageSize, "Message length was different from expected size.");
                        Debug.Assert(message[messageSize/2] == 0x42, "Message did not contain verification data.");
                    }

                    long ticks = watch.ElapsedTicks;
                    double seconds = (double)ticks/Stopwatch.Frequency;
                    double msgsPerSec = MsgCount/seconds;
                    double megabitsPerSec = msgsPerSec*messageSize*8/1000000;

                    Console.Out.WriteLine(" {0,-6} {1,10:0.0} {2,8:0.00}", messageSize, msgsPerSec, megabitsPerSec);
                }
            }
        }

        private void ProducerThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreatePushSocket())
            {
                socket.Connect("tcp://127.0.0.1:9091");

                foreach (var messageSize in s_messageSizes)
                {
                    var msg = new byte[messageSize];
                    msg[messageSize/2] = 0x42;

                    for (int i = 0; i < MsgCount; i++)
                        socket.Send(msg);
                }
            }
        }
    }
}