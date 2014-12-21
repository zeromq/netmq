using System;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace LazyPirate.Client
{
    class Program
    {
        private const int REQUEST_TIMEOUT = 2500;
        private const int REQUEST_RETRIES = 10;
        private const string SERVER_ENDPOINT = "tcp://127.0.0.1:5555";

        private static int _sequence = 0;
        private static bool _expectReply = true;
        private static int _retriesLeft = REQUEST_RETRIES;

        static void Main(string[] args)
        {
            using (var context = new Factory().CreateContext())
            {
                var client = CreateServerSocket(context);

                while (_retriesLeft > 0)
                {
                    _sequence++;
                    Console.WriteLine("C: Sending ({0})", _sequence);
                    client.Send(Encoding.Unicode.GetBytes(_sequence.ToString()));
                    _expectReply = true;

                    while (_expectReply)
                    {
                        bool result = client.Poll(TimeSpan.FromMilliseconds(REQUEST_TIMEOUT));

                        if (!result)
                        {
                            _retriesLeft--;

                            if (_retriesLeft == 0)
                            {
                                Console.WriteLine("C: Server seems to be offline, abandoning");
                                break;
                            }
                            else
                            {
                                Console.WriteLine("C: No response from server, retrying...");

                                TerminateClient(client);

                                client = CreateServerSocket(context);
                                client.Send(Encoding.Unicode.GetBytes(_sequence.ToString()));
                            }
                        }
                    }
                }
                TerminateClient(client);
            }
        }

		private static void TerminateClient(IRequestSocket client)
        {
            client.Disconnect(SERVER_ENDPOINT);
            client.Close();
        }

        private static IRequestSocket CreateServerSocket(INetMQContext context)
        {
            Console.WriteLine("C: Connecting to server...");

            var client = context.CreateRequestSocket();
            client.Connect(SERVER_ENDPOINT);
            client.Options.Linger = TimeSpan.Zero;
            client.ReceiveReady += ClientOnReceiveReady;

            return client;
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs netMqSocketEventArgs)
        {
            var reply = netMqSocketEventArgs.Socket.Receive();
            string strReply = Encoding.Unicode.GetString(reply);

            if (Int32.Parse(strReply) == _sequence)
            {
                Console.WriteLine("C: Server replied OK ({0})", strReply);
                _retriesLeft = REQUEST_RETRIES;
                _expectReply = false;
            }
            else
            {
                Console.WriteLine("C: Malformed reply from server: {0}", strReply);
            }
        }
    }
}
