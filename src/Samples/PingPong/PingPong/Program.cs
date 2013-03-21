using System;
using System.Threading;
using NetMQ;

namespace PingPong
{
    /*
     * Starts a server thread that prints out "Ping 1", "Ping 2", etc. ever 500ms
     * Starts a client thread that receives and prings the message
     * After receiving 10 messages the client thread notifies the server to stop
     */
    class Program
    {
        private static void Main(string[] args)
        {
            const string serverUri = "tcp://127.0.0.1:5000";

            ManualResetEvent allDone = new ManualResetEvent(false);
            Thread server = new Thread(() =>
                {
                    using (var context = NetMQContext.Create())
                    using (var publisher = context.CreatePublisherSocket())
                    {
                        publisher.Bind(serverUri);

                        int messageId = 0;

                        while (allDone.WaitOne(0) == false)
                        {
                            publisher.Send(string.Format("Ping {0}", messageId++));
                            Thread.Sleep(500);
                        }
                    }
                });

            Thread client = new Thread(() =>
                {
                    using (var context = NetMQContext.Create())
                    using (var subscriber = context.CreateSubscriberSocket())
                    {
                        subscriber.Connect(serverUri);

                        for (int i = 0; i < 10; i++)
                        {
                            subscriber.Subscribe("Ping");
                            Console.WriteLine(subscriber.ReceiveString());
                        }

                        allDone.Set();
                    }
                });

            server.Start();
            client.Start();

            server.Join();
            client.Join();
        }
    }
}
