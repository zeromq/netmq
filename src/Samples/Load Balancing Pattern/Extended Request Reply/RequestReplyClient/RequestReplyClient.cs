using System;
using NetMQ;

namespace ExtendedRequestReply
{
    internal static class RequestReplyClient
    {
        private const string ClientEndpoint = "tcp://127.0.0.1:5559";

        private static void Main()
        {
            using (var context = NetMQContext.Create())
            using (var client = context.CreateRequestSocket())
            {
                client.Connect(ClientEndpoint);

                for (var i = 0; i < 10; i++)
                {
                    var msg = new NetMQMessage();
                    msg.Append("Message_" + i);
                    client.SendMessage(msg);
                    Console.WriteLine("Sent Message {0}", msg.Last.ConvertToString());

                    var response = client.ReceiveMessage();
                    Console.WriteLine("Received Message {0}", response.Last.ConvertToString());
                }

                Console.ReadKey();
            }
        }
    }
}