using System;
using NetMQ;
using NetMQ.zmq;

namespace local_lat
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_lat <bind-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);

            var context = NetMQContext.Create();
            var repSocket = context.CreateResponseSocket();
            repSocket.Bind(bindTo);

            Msg message = new Msg();
            message.InitEmpty();

            for (int i = 0; i != roundtripCount; i++)
            {
                repSocket.Receive(ref message, SendReceiveOptions.None);
                if (message.Size != messageSize)
                {
                    Console.WriteLine("message of incorrect size received. Received: " + message.Size + " Expected: " + messageSize);
                    return -1;
                }

                repSocket.Send(ref message, SendReceiveOptions.None);
            }

            message.Close();
            repSocket.Close();
            context.Terminate();

            return 0;
        }
    }
}