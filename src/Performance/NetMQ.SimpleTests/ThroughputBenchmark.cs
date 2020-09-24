using System;
using System.Diagnostics;
using System.Threading;
using NetMQ.Sockets;

namespace NetMQ.SimpleTests
{
    internal abstract class ThroughputBenchmarkBase : ITest
    {
        private static readonly int[] s_messageSizes = { 8, 64, 256, 1024, 4096 };

        protected const int MsgCount = 1000000;

        public string? TestName { get; protected set; }

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
            using (var socket = CreateConsumerSocket())
            {
                socket.Bind("tcp://*:9091");

                foreach (var messageSize in s_messageSizes)
                {
                    var watch = Stopwatch.StartNew();

                    Consume(socket, messageSize);

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
            using (var socket = CreateProducerSocket())
            {
                socket.Connect("tcp://127.0.0.1:9091");

                foreach (var messageSize in s_messageSizes)
                    Produce(socket, messageSize);
            }
        }

        protected abstract PushSocket CreateProducerSocket();
        protected abstract PullSocket CreateConsumerSocket();

        protected abstract void Produce(PushSocket socket, int messageSize);
        protected abstract void Consume(PullSocket socket, int messageSize);
    }

    internal class ThroughputBenchmark : ThroughputBenchmarkBase
    {
        public ThroughputBenchmark()
        {
            TestName = "Push/Pull Throughput Benchmark";
        }

        protected override PushSocket CreateProducerSocket()
        {
            return new PushSocket();
        }

        protected override PullSocket CreateConsumerSocket()
        {
            return new PullSocket();
        }

        protected override void Produce(PushSocket socket, int messageSize)
        {
            var msg = new byte[messageSize];
            msg[messageSize/2] = 0x42;

            for (int i = 0; i < MsgCount; i++)
                socket.SendFrame(msg);
        }

        protected override void Consume(PullSocket socket, int messageSize)
        {
            for (int i = 0; i < MsgCount; i++)
            {
                var message = socket.ReceiveFrameBytes();
                Debug.Assert(message.Length == messageSize, "Message length was different from expected size.");
                Debug.Assert(message[messageSize/2] == 0x42, "Message did not contain verification data.");
            }
        }
    }

    internal class ThroughputBenchmarkReusingMsg : ThroughputBenchmarkBase
    {
        public ThroughputBenchmarkReusingMsg()
        {
            TestName = "Push/Pull Throughput Benchmark (reusing Msg)";
        }

        protected override PushSocket CreateProducerSocket()
        {
            return new PushSocket();
        }

        protected override PullSocket CreateConsumerSocket()
        {
            return new PullSocket();
        }

        protected override void Produce(PushSocket socket, int messageSize)
        {
            var msg = new Msg();

            for (int i = 0; i < MsgCount; i++)
            {
                msg.InitGC(new byte[messageSize], messageSize);
                msg.Slice()[messageSize / 2] = 0x42;

                socket.Send(ref msg, more: false);

                msg.Close();
            }
        }

        protected override void Consume(PullSocket socket, int messageSize)
        {
            var msg = new Msg();
            msg.InitEmpty();

            for (int i = 0; i < MsgCount; i++)
            {
                socket.Receive(ref msg);
                Debug.Assert(msg.Slice().Length == messageSize, "Message length was different from expected size.");
                Debug.Assert(msg.Slice()[msg.Size/2] == 0x42, "Message did not contain verification data.");
            }
        }
    }
}