using System;
using NetMQ;

namespace ExtendedRequestReply
{
    class RequestReplyClient
    {
        private const string CLIENT_ENDPOINT = "tcp://127.0.0.1:5559";

        static void Main(string[] args)
        {
            using (var ctx = NetMQContext.Create())
            {
                using (var client = ctx.CreateRequestSocket())
                {
                    client.Connect(CLIENT_ENDPOINT);
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
}
