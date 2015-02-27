using System;
using System.Diagnostics;
using System.Threading;

namespace NetMQ.SimpleTests
{
    internal class ThroughputBenchmark : ITest
    {
        private static readonly int[] MessageSizes = { 8, 64, 256, 1024, 4096 };

        private const int MsgCount = 1000000;

        public string TestName
        {
            get { return "Throughput Benchmark"; }
        }

        public void RunTest()
        {
            var proxyPull = new Thread(ProxyPullThread);
            var proxyPush = new Thread(ProxyPushThread);

            proxyPull.Start();
            proxyPush.Start();

            proxyPush.Join();
            proxyPull.Join();
        }

        private static void ProxyPullThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreatePullSocket())
            {
                socket.Bind("tcp://*:9091");

                foreach (var messageSize in MessageSizes)
                {
                    byte[] message = socket.Receive();
                    //Debug.Assert(receivedBytes == messageSize, "Message length was different from expected size.");
                    Debug.Assert(message[messageSize/2] == 0x42, "Message did not contain verification data.");

                    var watch = new Stopwatch();
                    watch.Start();

                    for (int i = 1; i < MsgCount; i++)
                    {
                        message = socket.Receive();
                        //Debug.Assert(receivedBytes == messageSize, "Message length was different from expected size.");
                        Debug.Assert(message[messageSize/2] == 0x42, "Message did not contain verification data.");
                    }

                    watch.Stop();

                    long elapsedTime = watch.ElapsedTicks;
                    long messageThroughput = MsgCount*Stopwatch.Frequency/elapsedTime;
                    long megabitThroughput = messageThroughput*messageSize*8/1000000;

                    Console.WriteLine("Message size: {0} [B]", messageSize);
                    Console.WriteLine("Average throughput: {0} [msg/s]", messageThroughput);
                    Console.WriteLine("Average throughput: {0} [Mb/s]", megabitThroughput);
                }
            }
        }

        private static void ProxyPushThread()
        {
            using (var context = NetMQContext.Create())
            using (var socket = context.CreatePushSocket())
            {
                socket.Connect("tcp://127.0.0.1:9091");

                foreach (var messageSize in MessageSizes)
                {
                    var msg = new byte[messageSize];
                    msg[messageSize/2] = 0x42;

                    for (int i = 0; i < MsgCount; i++)
                    {
                        socket.Send(msg);
                    }
                }
            }
        }
    }
}