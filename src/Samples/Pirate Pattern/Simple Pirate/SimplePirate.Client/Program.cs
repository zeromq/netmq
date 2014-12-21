using System;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace SimplePirate.Client
{
    class Program
    {
        private static int REQUEST_TIMEOUT = 2500;
        private static int REQUEST_RETRIES = 10;
        private static string SERVER_ENDPOINT = "tcp://localhost:5555";

        private static string _strSequenceSent = "";
        private static bool _expectReply = true;
        private static int _retriesLeft = 0;

        static void Main(string[] args)
        {
            _retriesLeft = REQUEST_RETRIES;

            using (var context = new Factory().CreateContext())
            {
                var client = CreateServerSocket(context);

                int sequence = 0;

                while (_retriesLeft > 0)
                {
                    sequence++;
                    _strSequenceSent = sequence.ToString() + " HELLO";
                    Console.WriteLine("C: Sending ({0})", _strSequenceSent);
                    client.Send(Encoding.Unicode.GetBytes(_strSequenceSent));
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
                                Console.WriteLine("C: No response from server, retrying..");

                                TerminateClient(client);

                                client = CreateServerSocket(context);
                                client.Send(Encoding.Unicode.GetBytes(_strSequenceSent));
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
            client.Options.Linger = TimeSpan.Zero;
            Guid guid = Guid.NewGuid();
            client.Options.Identity = Encoding.Unicode.GetBytes(guid.ToString());
            client.Connect(SERVER_ENDPOINT);
            client.ReceiveReady += ClientOnReceiveReady;

            return client;
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            var reply = socket.Socket.Receive();

            if (Encoding.Unicode.GetString(reply) == (_strSequenceSent + " WORLD!"))
            {
                Console.WriteLine("C: Server replied OK ({0})", Encoding.Unicode.GetString(reply));
                _retriesLeft = REQUEST_RETRIES;
                _expectReply = false;
            }
            else
            {
                Console.WriteLine("C: Malformed reply from server: {0}", Encoding.Unicode.GetString(reply));
            }
        }
    }
}
