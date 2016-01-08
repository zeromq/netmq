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
            var progressReporter = new Progress<string>(r => Console.WriteLine("C: " + r));
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var context = NetMQContext.Create())
            {
                while (true)
                {
                    var responseMessage = context.RequestResponseMultipartMessageWithRetry(ServerEndpoint, requestMessage, RequestRetries,
                        TimeSpan.FromMilliseconds(RequestTimeout), progressReporter);
                    if (responseMessage != null)
                    {
                        var strReply = responseMessage.First.ConvertToString();
                        Console.WriteLine("C: Server replied with ({0})", strReply);
                    }
                }
            }
        }
    }
}
