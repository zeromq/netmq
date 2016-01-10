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
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 3;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for message");
                            var message = rep.ReceiveMultipartMessage();

                            Console.WriteLine("ResponseEcho sending message");
                            rep.SendMultipartMessage(message);
                        }
                    }
                });
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 3;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for message2");
                            var message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message2 but never responds");
                            //Console.WriteLine("ResponseEcho sending message");
                            // never send rep.SendMultipartMessage(message);
                        }
                    }
                });
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                Assert.IsNull(responseMessage);
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 5;
            const int timeoutMsec = 100;
            var requestTimeout = TimeSpan.FromMilliseconds(timeoutMsec);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for message {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(timeoutMsec * 2));

                            rep.TrySendMultipartMessage(message);

                            // Now allow success
                            while (true)
                            {
                                Console.WriteLine("ResponseEcho waiting for message2 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveMultipartMessage();
                                Console.WriteLine("ResponseEcho received message2 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending message2 {0} at {1:ss:fff}", message.First.ConvertToString(), DateTime.Now);
                                rep.TrySendMultipartMessage(message);
                            }
                        }
                    }
                });
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout, progressPublisher);
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTryNullProgressReporter()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 5;
            const int timeoutMsec = 100;
            var requestTimeout = TimeSpan.FromMilliseconds(timeoutMsec);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for message {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(timeoutMsec * 2));

                            rep.TrySendMultipartMessage(message);

                            // Now allow success
                            while (true)
                            {
                                Console.WriteLine("ResponseEcho waiting for message2 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveMultipartMessage();
                                Console.WriteLine("ResponseEcho received message3 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending message3 {0} at {1:ss:fff}", message.First.ConvertToString(), DateTime.Now);
                                rep.TrySendMultipartMessage(message);
                            }
                        }
                    }
                });
                var responseMessage = RequestSocket.RequestResponseMultipartMessageWithRetry(address, requestMessage, numTries, requestTimeout);
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(1, responseMessage.FrameCount);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual("Hi", responseString);
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsFirstTry()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 3;
            const string requestString = "Hi";
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for string at {0:ss.fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();

                            Console.WriteLine("ResponseEcho sending string:{0} at {1:ss.fff}", message, DateTime.Now);
                            rep.SendFrame(message);
                        }
                    }
                });
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout, progressPublisher);
                Assert.AreEqual(requestString, responseString);
            }
        }

        [Test]
        public void RequestResponseStringWithRetryFails()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 3;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            const string requestString = "Hi";
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for string2");
                            var message = rep.ReceiveFrameString();
                            Console.WriteLine("ResponseEcho received string2 but never responds");
                            // never send rep.SendFrame(message);
                        }
                    }
                });
                var responseMessage = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout);
                Assert.IsNull(responseMessage);
            }
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 5;
            const int timeoutMsec = 100;
            var requestTimeout = TimeSpan.FromMilliseconds(timeoutMsec);
            const string requestString = "Hi";
            using (var progressPublisher = new PublisherSocket())
            {
                progressPublisher.Bind("tcp://127.0.0.1:5556");
                Task.Factory.StartNew(() =>
                {
                    using (var progressSubscriber = new SubscriberSocket())
                    {
                        progressSubscriber.Connect("tcp://127.0.0.1:5556");
                        progressSubscriber.SubscribeToAnyTopic();
                        while (true)
                        {
                            var topic = progressSubscriber.ReceiveFrameString();
                            Console.WriteLine("C: {0} {1:ss.fff}", topic, DateTime.Now);
                        }
                    }
                });
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for string {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();
                            Console.WriteLine("ResponseEcho received string {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(timeoutMsec * 2));

                            rep.TrySendFrame(message);

                            // Now allow success
                            while (true)
                            {
                                Console.WriteLine("ResponseEcho waiting for string4 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveFrameString();
                                Console.WriteLine("ResponseEcho received string4 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending string5 {0} at {1:ss:fff}", message, DateTime.Now);
                                rep.TrySendFrame(message);
                            }
                        }
                    }
                });
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout, progressPublisher);
                Assert.IsNotNull(responseString);
                Assert.AreEqual(requestString, responseString);
            }
        }

        [Test]
        public void RequestResponseStringWithRetrySucceedsNotOnFirstTryNullProgressReporter()
        {
            const string address = "tcp://127.0.0.1:5555";
            const int numTries = 5;
            const int timeoutMsec = 100;
            var requestTimeout = TimeSpan.FromMilliseconds(timeoutMsec);
            const string requestString = "Hi";
                Task.Factory.StartNew(() =>
                {
                    using (var rep = new ResponseSocket())
                    {
                        rep.Bind(address);
                        while (true)
                        {
                            Console.WriteLine("ResponseEcho waiting for string {0:ss:fff}", DateTime.Now);
                            var message = rep.ReceiveFrameString();
                            Console.WriteLine("ResponseEcho received string {0:ss:fff}", DateTime.Now);

                            // Force retry
                            Thread.Sleep(TimeSpan.FromMilliseconds(timeoutMsec * 2));

                            rep.TrySendFrame(message);

                            // Now allow success
                            while (true)
                            {
                                Console.WriteLine("ResponseEcho waiting for string2 {0:ss:fff}", DateTime.Now);
                                message = rep.ReceiveFrameString();
                                Console.WriteLine("ResponseEcho received string2 {0:ss:fff}", DateTime.Now);

                                Console.WriteLine("ResponseEcho sending string2 {0} at {1:ss:fff}", message, DateTime.Now);
                                rep.TrySendFrame(message);
                            }
                        }
                    }
                });
                var responseString = RequestSocket.RequestResponseStringWithRetry(address, requestString, numTries, requestTimeout);
                Assert.IsNotNull(responseString);
                Assert.AreEqual(requestString, responseString);
        }
    }
}
