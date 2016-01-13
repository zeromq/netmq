using System;
using NetMQ;
using NetMQ.Sockets;

namespace local_lat
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_lat <bind-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);

            using (var rep = new ResponseSocket())
            {
                rep.Bind(bindTo);

                var msg = new Msg();
                msg.InitEmpty();

                for (int i = 0; i != roundtripCount; i++)
                {
                    rep.Receive(ref msg);
                    if (msg.Size != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + msg.Size + " Expected: " + messageSize);
                        return -1;
                    }

                    rep.Send(ref msg, more: false);
                }

                msg.Close();
            }

            return 0;
        }
    }
}