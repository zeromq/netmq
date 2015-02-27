using System;
using NetMQ;
using NetMQ.zmq;

namespace remote_thr
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_thr <connect-to> <message-size> <message-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            using (var context = NetMQContext.Create())
            using (var push = context.CreatePushSocket())
            {
                push.Connect(connectTo);

                for (int i = 0; i != messageCount; i++)
                {
                    var message = new Msg();
                    message.InitPool(messageSize);
                    push.Send(ref message, SendReceiveOptions.None);
                    message.Close();
                }
            }

            return 0;
        }
    }
}
