namespace ZeroMQ.SimpleTests
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    internal class ReceiveBenchmark : ITest
    {
        private const int RoundtripCount = 10000;

        private static readonly int[] MessageSizes = { 8, 64, 512, 4096, 8192, 16384, 32768 };

        private readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);

        public string TestName
        {
            get { return "Receive Benchmark"; }
        }

        public void RunTest()
        {
            using (var context = ZmqContext.Create())
            {
                var client = new Thread(ClientThread);
                var server = new Thread(ServerThread);

                server.Start(context);
                client.Start(context);

                server.Join();
                client.Join();
            }
        }

        private void ClientThread(object contextObj)
        {
            var context = (ZmqContext)contextObj;
            using (var socket = context.CreateSocket(SocketType.REQ))
            {
                _readyEvent.WaitOne();

                socket.Connect("tcp://localhost:9000");

                Console.WriteLine("Receive(Frame)");

                foreach (int messageSize in MessageSizes)
                {
                    var msg = new Frame(messageSize);
                    var reply = new Frame(messageSize);

                    var watch = new Stopwatch();
                    watch.Start();

                    for (int i = 0; i < RoundtripCount; i++)
                    {
                        SendStatus sendStatus = socket.SendFrame(msg);

                        Debug.Assert(sendStatus == SendStatus.Sent, "Message was not indicated as sent.");

                        reply = socket.ReceiveFrame(reply);

                        Debug.Assert(reply.MessageSize == messageSize, "Pong message did not have the expected size.");
                    }

                    watch.Stop();
                    long elapsedTime = watch.ElapsedTicks;

                    Console.WriteLine("Message size: " + messageSize + " [B]");
                    Console.WriteLine("Roundtrips: " + RoundtripCount);

                    double latency = (double)elapsedTime / RoundtripCount / 2 * 1000000 / Stopwatch.Frequency;
                    Console.WriteLine("Your average latency is {0} [us]", latency.ToString("f2"));
                }

                Console.WriteLine("Receive(byte[])");

                foreach (int messageSize in MessageSizes)
                {
                    var msg = new Frame(messageSize);
                    var reply = new byte[messageSize];

                    var watch = new Stopwatch();
                    watch.Start();

                    for (int i = 0; i < RoundtripCount; i++)
                    {
                        SendStatus sendStatus = socket.SendFrame(msg);

                        Debug.Assert(sendStatus == SendStatus.Sent, "Message was not indicated as sent.");

                        int bytesReceived = socket.Receive(reply);

                        Debug.Assert(bytesReceived == messageSize, "Pong message did not have the expected size.");
                    }

                    watch.Stop();
                    long elapsedTime = watch.ElapsedTicks;

                    Console.WriteLine("Message size: " + messageSize + " [B]");
                    Console.WriteLine("Roundtrips: " + RoundtripCount);

                    double latency = (double)elapsedTime / RoundtripCount / 2 * 1000000 / Stopwatch.Frequency;
                    Console.WriteLine("Your average latency is {0} [us]", latency.ToString("f2"));
                }
            }
        }

        private void ServerThread(object contextObj)
        {
            var context = (ZmqContext)contextObj;
            using (var socket = context.CreateSocket(SocketType.REP))
            {
                socket.Bind("tcp://*:9000");

                _readyEvent.Set();

                for (int j = 0; j < 2; j++)
                {
                    foreach (int messageSize in MessageSizes)
                    {
                        for (int i = 0; i < RoundtripCount; i++)
                        {
                            Frame message = socket.ReceiveFrame();

                            Debug.Assert(message.ReceiveStatus == ReceiveStatus.Received, "Ping message result was non-successful.");
                            Debug.Assert(message.MessageSize == messageSize, "Ping message length did not match expected value.");

                            socket.SendFrame(message);
                        }
                    }
                }
            }
        }
    }
}
