using System;
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

            var context = ZMQ.CtxNew();
            var repSocket = ZMQ.Socket(context, ZmqSocketType.Rep);
            repSocket.Bind(bindTo);
            
            for (int i = 0; i != roundtripCount; i++)
            {
                Msg message = repSocket.Recv(SendReceiveOptions.None);
                if (ZMQ.MsgSize(message) != messageSize)
                {
                    Console.WriteLine("message of incorrect size received. Received: " + message.Size + " Expected: " + messageSize);
                    return -1;
                }

                repSocket.Send(message, SendReceiveOptions.None);                
            }

            repSocket.Close();
            context.Terminate();

            return 0;
        }
    }
}