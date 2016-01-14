using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace LazyPirate.Client2
{
    /// <summary>
    /// Run both LazyPirate.Server2 and LazyPirate.Client2 simultaneously to see the demonstration of this pattern.
    /// LazyPirate.Client and LazyPirate.Client2 are functionally equivalent. The first uses UniCode strings, the second uses ASCII strings.
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "NetMQ LazyPirate Client 2";
            const string serverAddress = "tcp://127.0.0.1:5555";
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(2500);
            var requestRetries = 10;
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var progressPublisher = new PublisherSocket())
            {
                const string pubSubAddress = "tcp://127.0.0.1:5556";
                progressPublisher.Bind(pubSubAddress);
                SubscriberContinuousLoop(pubSubAddress, requestString);
                while (true)
                {
                    var responseString = RequestSocket.RequestResponseMultipartMessageWithRetry(serverAddress, requestMessage, 
                        requestRetries, requestTimeout, progressPublisher);
                }
            }
        }

        private static void SubscriberContinuousLoop(string pubSubAddress, string requestString)
        {
            Task.Factory.StartNew(() =>
            {
                using (var progressSubscriber = new SubscriberSocket())
                {
                    progressSubscriber.Connect(pubSubAddress);
                    progressSubscriber.SubscribeToAnyTopic();
                    while (true)
                    {
                        var topic = progressSubscriber.ReceiveFrameString();
                        RequestSocket.ProgressTopic progressTopic;
                        Enum.TryParse(topic, out progressTopic);
                        switch (progressTopic)
                        {
                            case RequestSocket.ProgressTopic.Send:
                                Console.WriteLine("C: Sending {0}", requestString);
                                break;
                            case RequestSocket.ProgressTopic.Retry:
                                Console.WriteLine("C: No response from server, retrying...");
                                break;
                            case RequestSocket.ProgressTopic.Failure:
                                Console.WriteLine("C: Server seems to be offline, abandoning");
                                break;
                            case RequestSocket.ProgressTopic.Success:
                                Console.WriteLine("C: Server replied OK");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }


                    }
                }
            });
        }
    }
}
