using System;
using NetMQ;
using NetMQ.zmq;

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

            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            {
                rep.Bind(bindTo);

                var message = new Msg();
                message.InitEmpty();

                for (int i = 0; i != roundtripCount; i++)
                {
                    rep.Receive(ref message, SendReceiveOptions.None);
                    if (message.Size != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + message.Size + " Expected: " + messageSize);
                        return -1;
                    }

                    rep.Send(ref message, SendReceiveOptions.None);
                }

                message.Close();
            }

            return 0;
        }
    }
}