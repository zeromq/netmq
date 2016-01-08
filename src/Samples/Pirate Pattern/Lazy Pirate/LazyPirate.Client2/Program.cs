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
        private const string ServerAddress = "tcp://127.0.0.1:5555";

        static void Main(string[] args)
        {
            Console.Title = "NetMQ LazyPirate Client 2";
            var progressReporter = new Progress<string>(r => Console.WriteLine("C: " + r));
            using (var context = NetMQContext.Create())
            {
                while (true)
                {
                    var responseString = context.RequestResponseStringWithRetry(ServerAddress, "Hi", RequestRetries,
                        TimeSpan.FromMilliseconds(RequestTimeout), progressReporter);
                }
            }
        }
    }
}
