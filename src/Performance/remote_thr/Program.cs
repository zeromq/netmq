using System;
using NetMQ.zmq;

namespace remote_thr
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_thr <connect-to> <message-size> <message-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            var context = ZMQ.CtxNew();
            var pushSocket = ZMQ.Socket(context, ZmqSocketType.Push);
            pushSocket.Connect(connectTo);

            for (int i = 0; i != messageCount; i++)
            {
                var message = new Msg(messageSize);
                pushSocket.Send(message, SendReceiveOptions.None);
            }

            pushSocket.Close();
            context.Terminate();

            return 0;
        }
    }
}
