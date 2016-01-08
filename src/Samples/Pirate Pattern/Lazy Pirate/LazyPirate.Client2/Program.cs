using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;

namespace LazyPirate.Client2
{
    internal static class Program
    {
        private const int RequestTimeout = 2500;
        private const int RequestRetries = 10;
        private const string ServerEndpoint = "tcp://127.0.0.1:5555";

        static void Main(string[] args)
        {
            Console.Title = "NetMQ LazyPirate Client 2";
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var context = NetMQContext.Create())
            {
                while (true)
                {
                    var responseMessage = context.RequestResponseMultipartMessageWithRetry(ServerEndpoint, requestMessage, RequestRetries,
                        TimeSpan.FromMilliseconds(RequestTimeout), true);
                    if (responseMessage != null)
                    {
                        var strReply = responseMessage.First.ConvertToString();
                        Console.WriteLine("C: Server replied OK ({0})", strReply);
                    }
                    else
                    {
                        Console.WriteLine("C: Server seems to be offline, abandoning");
                        break;
                    }
                }
            }
        }
    }
}
