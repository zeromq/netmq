#if !NET35
using System;
using System.Diagnostics;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class RequestWithRetryTests : IClassFixture<CleanupAfterFixture>
    {
        public RequestWithRetryTests() => NetMQConfig.Cleanup();

        [Fact]
        public void RequestResponseMultipartMessageWithRetrySucceedsFirstTry()
        {
            const string address = "tcp://127.0.0.1:51001";
            const string pubAddress = "tcp://127.0.0.1:60001";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");

            using (var progressPublisher = new PublisherSocket(pubAddress))
            using (var progressSubscriber = new SubscriberSocket(pubAddress))
            using (var server = new ResponseSocket(address))
            {
                progressSubscriber.SubscribeToAnyTopic();
                var progressProactor = new NetMQProactor(progressSubscriber, (socket, message) => Debug.WriteLine("C: {0} {1:ss.fff}", message[0].ConvertToString(), DateTime.Now));

                var serverProactor = new NetMQProactor(server, (socket, message) =>
                {
                    Debug.WriteLine("ResponseEcho received message {0} at {1:ss.fff}", message.First.ConvertToString(),
                        DateTime.Now);

                    // reply same message
                    socket.SendMultipartMessage(message);
                });

                using (serverProactor)
                using (progressProactor)
                {
                    var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address,
                        requestMessage, numTries, requestTimeout, progressPublisher);
                    Assert.NotNull(responseMessage);
                    Assert.Equal(1, responseMessage!.FrameCount);
                    var responseString = responseMessage.First.ConvertToString();
                    Assert.Equal("Hi", responseString);
                }
            }
        }

        [Fact]
        public void RequestResponseMultipartMessageWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:51002";
            const string pubAddress = "tcp://127.0.0.1:60002";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");

            using (var progressPublisher = new PublisherSocket(pubAddress))
            using (var progressSubscriber = new SubscriberSocket(pubAddress))
            using (var server = new RouterSocket(address))
            {
                progressSubscriber.SubscribeToAnyTopic();
                var progressProactor = new NetMQProactor(progressSubscriber, (socket, message) =>
                    Debug.WriteLine("C: {0} {1:ss.fff}", message[0].ConvertToString(), DateTime.Now));

                var serverProactor = new NetMQProactor(server, (socket, message) =>
                {
                    Debug.WriteLine("ResponseEcho received message {0} at {1:ss.fff}", message[2].ConvertToString(),
                        DateTime.Now);
                });

                using (serverProactor)
                using (progressProactor)
                {
                    var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage,
                        numTries, requestTimeout, progressPublisher);
                    Assert.Null(responseMessage);
                }
            }
        }

        [Fact]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:51003";
            const string pubAddress = "tcp://127.0.0.1:60003";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");

            using (var progressPublisher = new PublisherSocket(pubAddress))
            using (var progressSubscriber = new SubscriberSocket(pubAddress))
            using (var server = new RouterSocket(address))
            {
                progressSubscriber.SubscribeToAnyTopic();
                var progressProactor = new NetMQProactor(progressSubscriber, (socket, message) =>
                    Debug.WriteLine("C: {0} {1:ss.fff}", message[0].ConvertToString(), DateTime.Now));

                int attempt = 0;

                var serverProactor = new NetMQProactor(server, (socket, message) =>
                {
                    Debug.WriteLine("ResponseEcho received message {0} at {1:ss.fff}", message[2].ConvertToString(),
                        DateTime.Now);

                    attempt++;

                    if (attempt > 1)
                    {
                        // reply same message
                        socket.SendMultipartMessage(message);
                    }
                });

                using (serverProactor)
                using (progressProactor)
                {
                    var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address,
                        requestMessage, numTries, requestTimeout, progressPublisher);
                    Assert.NotNull(responseMessage);
                    Assert.Equal(1, responseMessage!.FrameCount);
                    var responseString = responseMessage.First.ConvertToString();
                    Assert.Equal("Hi", responseString);
                }
            }
        }

        [Fact]
        public void RequestResponseStringWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:51004";
            const string pubAddress = "tcp://127.0.0.1:60004";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);

            using (var progressPublisher = new PublisherSocket(pubAddress))
            using (var progressSubscriber = new SubscriberSocket(pubAddress))
            using (var server = new RouterSocket(address))
            {
                progressSubscriber.SubscribeToAnyTopic();
                var progressProactor = new NetMQProactor(progressSubscriber, (socket, message) =>
                    Debug.WriteLine("C: {0} {1:ss.fff}", message[0].ConvertToString(), DateTime.Now));

                var serverProactor = new NetMQProactor(server, (socket, message) =>
                {
                    Debug.WriteLine("ResponseEcho received message {0} at {1:ss.fff}", message[2].ConvertToString(),
                        DateTime.Now);
                });

                using (serverProactor)
                using (progressProactor)
                {
                    var responseMessage = RequestSocket.RequestResponseStringWithRetry(address, "Hi",
                        numTries, requestTimeout, progressPublisher);
                    Assert.Null(responseMessage);
                }
            }
        }

        [Fact]
        public void RequestResponseStringWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:51005";
            const string pubAddress = "tcp://127.0.0.1:60005";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);

            using (var progressPublisher = new PublisherSocket(pubAddress))
            using (var progressSubscriber = new SubscriberSocket(pubAddress))
            using (var server = new RouterSocket(address))
            {
                progressSubscriber.SubscribeToAnyTopic();
                var progressProactor = new NetMQProactor(progressSubscriber, (socket, message) =>
                    Debug.WriteLine("C: {0} {1:ss.fff}", message[0].ConvertToString(), DateTime.Now));

                int attempt = 0;

                var serverProactor = new NetMQProactor(server, (socket, message) =>
                {
                    Debug.WriteLine("ResponseEcho received message {0} at {1:ss.fff}", message[2].ConvertToString(),
                        DateTime.Now);

                    attempt++;

                    if (attempt > 1)
                    {
                        // reply same message
                        socket.SendMultipartMessage(message);
                    }
                });

                using (serverProactor)
                using (progressProactor)
                {
                    var responseMessage = RequestSocket.RequestResponseStringWithRetry(address,
                        "Hi", numTries, requestTimeout, progressPublisher);
                    Assert.Equal("Hi", responseMessage);
                }
            }
        }
    }
}
#endif