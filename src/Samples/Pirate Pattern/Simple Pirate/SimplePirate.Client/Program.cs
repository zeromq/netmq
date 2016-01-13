using System;
using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace SimplePirate.Client
{
    internal static class Program
    {
        private const int RequestTimeout = 2500;
        private const int RequestRetries = 10;
        private const string ServerEndpoint = "tcp://localhost:5555";

        private static string _strSequenceSent = "";
        private static bool _expectReply = true;
        private static int _retriesLeft = 0;

        private static void Main()
        {
            _retriesLeft = RequestRetries;

            var client = CreateServerSocket();

            int sequence = 0;

            while (_retriesLeft > 0)
            {
                sequence++;
                _strSequenceSent = sequence.ToString() + " HELLO";
                Console.WriteLine("C: Sending ({0})", _strSequenceSent);
                client.SendFrame(Encoding.Unicode.GetBytes(_strSequenceSent));
                _expectReply = true;

                while (_expectReply)
                {
                    bool result = client.Poll(TimeSpan.FromMilliseconds(RequestTimeout));

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

                            client = CreateServerSocket();
                            client.SendFrame(Encoding.Unicode.GetBytes(_strSequenceSent));
                        }
                    }
                }
            }
            TerminateClient(client);
        }

        private static void TerminateClient(RequestSocket client)
        {
            client.Disconnect(ServerEndpoint);
            client.Close();
        }

        private static RequestSocket CreateServerSocket()
        {
            Console.WriteLine("C: Connecting to server...");

            var guid = Guid.NewGuid();
            var client = new RequestSocket
            {
                Options =
                {
                    Linger = TimeSpan.Zero,
                    Identity = Encoding.Unicode.GetBytes(guid.ToString())
                }
            };
            client.Connect(ServerEndpoint);
            client.ReceiveReady += ClientOnReceiveReady;

            return client;
        }

        private static void ClientOnReceiveReady(object sender, NetMQSocketEventArgs socket)
        {
            var reply = socket.Socket.ReceiveFrameBytes();

            if (Encoding.Unicode.GetString(reply) == (_strSequenceSent + " WORLD!"))
            {
                Console.WriteLine("C: Server replied OK ({0})", Encoding.Unicode.GetString(reply));
                _retriesLeft = RequestRetries;
                _expectReply = false;
            }
            else
            {
                Console.WriteLine("C: Malformed reply from server: {0}", Encoding.Unicode.GetString(reply));
            }
        }
    }
}