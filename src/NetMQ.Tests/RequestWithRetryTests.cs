using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class RequestWithRetryTests
    {
        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsFirstTry()
        {
            const string address = "tcp://127.0.0.1:50001";
            const string pubAddress = "tcp://127.0.0.1:60001";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for message at {0:ss.fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();

                            Console.WriteLine("ResponseEcho sending message:{0} at {1:ss.fff}", message, DateTime.Now);
                            rep.SendFrame(message);
                        }
                    }
                }, cts.Token);
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                cts.Cancel();
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:50002";
            const string pubAddress = "tcp://127.0.0.1:60002";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for message at {0:ss.fff}", DateTime.Now);
                            rep.ReceiveFrameString();
                        }
                    }
                }, cts.Token);
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                cts.Cancel();
                Assert.IsNull(responseMessage);
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:50003";
            const string pubAddress = "tcp://127.0.0.1:60003";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for message {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(requestTimeout.TotalMilliseconds * 2));

                            rep.TrySendMultipartMessage(message);

                            // Now allow success
                            while (!cts.Token.IsCancellationRequested)
                            {
                                Console.WriteLine("ResponseEcho waiting for message2 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveMultipartMessage();
                                Console.WriteLine("ResponseEcho received message2 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending message2 {0} at {1:ss:fff}", message.First.ConvertToString(), DateTime.Now);
                                rep.TrySendMultipartMessage(message);
                            }
                        }
                    }
                }, cts.Token);
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout, progressPublisher);
                cts.Cancel();
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTryNullProgressPublisher()
        {
            const string address = "tcp://127.0.0.1:50004";
            const int numTries = 5;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            var cts = new CancellationTokenSource();
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for message {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(requestTimeout.TotalMilliseconds * 2));

                            rep.TrySendMultipartMessage(message);

                            // Now allow success
                            while (!cts.Token.IsCancellationRequested)
                            {
                                Console.WriteLine("ResponseEcho waiting for message2 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveMultipartMessage();
                                Console.WriteLine("ResponseEcho received message2 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending message2 {0} at {1:ss:fff}", message.First.ConvertToString(), DateTime.Now);
                                rep.TrySendMultipartMessage(message);
                            }
                        }
                    }
                }, cts.Token);
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                cts.Cancel();
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
            }
            finally
            {
                cts.Dispose();
            }
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsFirstTryNullProgressPublisher()
        {
            const string address = "tcp://127.0.0.1:50005";
            const int numTries = 5;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var cts = new CancellationTokenSource();
            try
            {
                using (var rep = new ResponseSocket())
                {
                    rep.Bind(address);
                    Task.Factory.StartNew(() =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for string at {0:ss.fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();

                            Console.WriteLine("ResponseEcho sending string:{0} at {1:ss.fff}", message, DateTime.Now);
                            rep.SendFrame(message);
                        }
                    }, cts.Token);
                    var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout);
                    cts.Cancel();
                    Assert.AreEqual(requestString, responseString);
                }
            }
            finally
            {
                cts.Dispose();
            }
        }
        
        [Test]
        public void RequestResponseStringWithRetrySucceedsFirstTry()
        {
            const string address = "tcp://127.0.0.1:50006";
            const string pubAddress = "tcp://127.0.0.1:60006";
            const int numTries = 5;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                using (var rep = new ResponseSocket())
                {
                    rep.Bind(address);
                    Task.Factory.StartNew(() =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for string at {0:ss.fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();

                            Console.WriteLine("ResponseEcho sending string:{0} at {1:ss.fff}", message, DateTime.Now);
                            rep.SendFrame(message);
                        }
                    }, cts.Token);
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout, progressPublisher);
                cts.Cancel();
                Assert.AreEqual(requestString, responseString);
                }
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }

        [Test]
        public void RequestResponseStringWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:50007";
            const string pubAddress = "tcp://127.0.0.1:60007";
            const int numTries = 5;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for string at {0:ss.fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();

                            //Console.WriteLine("ResponseEcho sending string:{0} at {1:ss.fff}", message, DateTime.Now);
                            //rep.SendFrame(message);
                        }
                    }
                }, cts.Token)
                    ;
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout, progressPublisher);
                cts.Cancel();
                Assert.IsNull(responseString);
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsNotOnFirstTryNullProgressPublisher()
        {
            const string address = "tcp://127.0.0.1:50008";
            const int numTries = 5;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var cts = new CancellationTokenSource();
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for string {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();
                            Console.WriteLine("ResponseEcho received string {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(requestTimeout.TotalMilliseconds * 2));

                            rep.TrySendFrame(message);

                            // Now allow success
                            while (!cts.Token.IsCancellationRequested)
                            {
                                Console.WriteLine("ResponseEcho waiting for string4 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveFrameString();
                                Console.WriteLine("ResponseEcho received string4 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending string5 {0} at {1:ss:fff}", message, DateTime.Now);
                                rep.TrySendFrame(message);
                            }
                        }
                    }
                }, cts.Token);
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout);
                cts.Cancel();
                Assert.AreEqual(requestString, responseString);
            }
            finally
            {
                cts.Dispose();
            }
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:50009";
            const string pubAddress = "tcp://127.0.0.1:60009";
            const int numTries = 10;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var cts = new CancellationTokenSource();
            var progressPublisher = new PublisherSocket();
            progressPublisher.Bind(pubAddress);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect(pubAddress);
                        progressSubscriber.SubscribeToAnyTopic();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                }, cts.Token);
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for string {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();
                            Console.WriteLine("ResponseEcho received string {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(requestTimeout.TotalMilliseconds * 2));

                            rep.TrySendFrame(message);

                            // Now allow success
                            while (!cts.Token.IsCancellationRequested)
                            {
                                Console.WriteLine("ResponseEcho waiting for string4 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveFrameString();
                                Console.WriteLine("ResponseEcho received string4 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending string5 {0} at {1:ss:fff}", message, DateTime.Now);
                                rep.TrySendFrame(message);
                            }
                        }
                    }
                }, cts.Token);
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout, progressPublisher);
                cts.Cancel();
                Assert.AreEqual(requestString, responseString);
            }
            finally
            {
                cts.Dispose();
                progressPublisher.Dispose();
            }
        }


    }
}
