using System;
using NetMQ;
using NetMQ.Sockets;

namespace ExtendedRequestReply
{
    internal static class RequestReplyClient
    {
        private static void Main()
        {
            using (var client = new RequestSocket(">tcp://127.0.0.1:5559"))
            {
                for (var i = 0; i < 10; i++)
                {
                    var msg = new NetMQMessage();
                    msg.Append("Message_" + i);
                    client.SendMultipartMessage(msg);
                    Console.WriteLine("Sent Message {0}", msg.Last.ConvertToString());

                    var response = client.ReceiveMultipartMessage();
                    Console.WriteLine("Received Message {0}", response.Last.ConvertToString());
                }

                Console.ReadKey();
            }
        }
    }
}