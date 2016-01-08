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
    public class NetMQContextExtensionsTests
    {
        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsFirstTry()
        {
            const string address = "tcp://127.0.0.1";
            const int numTries = 3;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var cts = new CancellationTokenSource())
            {
                var port = rep.BindRandomPort(address);
                var addressWithPort = string.Format("{0}:{1}", address, port);
                Task.Factory.StartNew(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("ResponseEcho waiting for message1");
                        var message = rep.ReceiveMultipartMessage();
                        Console.WriteLine("ResponseEcho sending message1");
                        rep.SendMultipartMessage(message);
                    }
                }, cts.Token);
                var responseMessage = context.RequestResponseMultipartMessageWithRetry(addressWithPort, requestMessage, numTries, requestTimeout);
                //cts.Cancel();
                Assert.AreEqual(responseMessage.FrameCount, 1);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual(responseString, "Hi");
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetryFails()
        {
            const string address = "tcp://127.0.0.1";
            const int numTries = 3;
            var requestTimeout = TimeSpan.FromMilliseconds(100);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var cts = new CancellationTokenSource())
            {
                var port = rep.BindRandomPort(address);
                var addressWithPort = string.Format("{0}:{1}", address, port);
                Task.Factory.StartNew(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("ResponseEcho waiting for message2");
                        var message = rep.ReceiveMultipartMessage();
                        Console.WriteLine("ResponseEcho received message2 but never responds");
                        //Console.WriteLine("ResponseEcho sending message");
                        // never send rep.SendMultipartMessage(message);
                    }
                }, cts.Token);
                var responseMessage = context.RequestResponseMultipartMessageWithRetry(addressWithPort, requestMessage, numTries, requestTimeout);
                //cts.Cancel();
                Assert.IsNull(responseMessage);
            }
        }

        [Test]
        public void RequestResponseMultipartMessageWithRetrySucceedsNotOnFirstTry()
        {
            const string address = "tcp://127.0.0.1";
            const int numTries = 5;
            const int timeoutMsec = 100;
            var requestTimeout = TimeSpan.FromMilliseconds(timeoutMsec);
            var requestMessage = new NetMQMessage(1);
            requestMessage.Append("Hi");
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var cts = new CancellationTokenSource())
            {
                var port = rep.BindRandomPort(address);
                var addressWithPort = string.Format("{0}:{1}", address, port);
                Task.Factory.StartNew(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("ResponseEcho waiting for message3 {0:ss:fff}", DateTime.Now);
                        var message = rep.ReceiveMultipartMessage();
                        Console.WriteLine("ResponseEcho received message3 {0:ss:fff}", DateTime.Now);

                        // Force retry
                        Thread.Sleep(TimeSpan.FromMilliseconds(timeoutMsec * 2));

                        rep.TrySendMultipartMessage(message);

                        // Now allow success
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("ResponseEcho waiting for message4 {0:ss:fff}", DateTime.Now);
                            message = rep.ReceiveMultipartMessage();
                            Console.WriteLine("ResponseEcho received message4 {0:ss:fff}", DateTime.Now);

                            Console.WriteLine("ResponseEcho sending message5 {0} at {1:ss:fff}", message.First.ConvertToString(), DateTime.Now);
                            //rep.SendMultipartMessage(message);
                            rep.TrySendMultipartMessage(message);
                        }
                    }
                }, cts.Token);
                var progressReporter = new Progress<string>(r => Console.WriteLine("C: " + r));
                var responseMessage = context.RequestResponseMultipartMessageWithRetry(addressWithPort, requestMessage,
                    numTries, requestTimeout, progressReporter);
                //cts.Cancel();
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(responseMessage.FrameCount, 1);
                var responseString = responseMessage.First.ConvertToString();
                Assert.AreEqual(responseString, "Hi");
            }
        }
    }
}
