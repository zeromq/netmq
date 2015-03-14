using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class MonitorPollTests
    {
        [Test]
        public void Monitoring()
        {
            using (var context = NetMQContext.Create())
            using (var rep = context.CreateResponseSocket())
            using (var req = context.CreateRequestSocket())
            using (var monitor = new NetMQMonitor(context, rep, "inproc://rep.inproc", SocketEvent.Accepted | SocketEvent.Listening))
            {
                bool listening = false;
                bool accepted = false;

                monitor.Accepted += (s, a) => { accepted = true; };
                monitor.Listening += (s, a) => { listening = true; };

                monitor.Timeout = TimeSpan.FromMilliseconds(100);

                var pollerTask = Task.Factory.StartNew(monitor.Start);

                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);
                req.Send("a");

                rep.SkipFrame();

                rep.Send("b");

                req.SkipFrame();

                Thread.Sleep(200);

                Assert.IsTrue(listening);
                Assert.IsTrue(accepted);

                monitor.Stop();

                Thread.Sleep(200);

                Assert.IsTrue(pollerTask.IsCompleted);
            }
        }

        [Test]
        public void ErrorCodeTest()
        {
            bool eventArrived = false;

            using (var context = NetMQContext.Create())
            using (var req = context.CreateRequestSocket())
            using (var rep = context.CreateResponseSocket())
            using (var monitor = new NetMQMonitor(context, req, "inproc://rep.inproc", SocketEvent.ConnectDelayed))
            {
                monitor.ConnectDelayed += (s, a) => { eventArrived = true; };

                monitor.Timeout = TimeSpan.FromMilliseconds(100);

                var pollerTask = Task.Factory.StartNew(monitor.Start);

                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);
                req.Send("a");

                rep.SkipFrame();

                rep.Send("b");

                req.SkipFrame();

                Thread.Sleep(200);

                Assert.IsTrue(eventArrived);

                monitor.Stop();

                Thread.Sleep(200);

                Assert.IsTrue(pollerTask.IsCompleted);
            }
        }

        [Test]
        public void MonitorDisposeProperlyWhenDisposedAfterMonitoredTcpSocket()
        {
            // The bug:
            // Given we monitor a netmq tcp socket
            // Given we disposed of the monitored socket first
            // When we dispose of the monitor
            // Then our monitor is Faulted with a EndpointNotFoundException
            // And monitor can't be stopped or disposed

            using (var theContext = NetMQContext.Create())
            using (var resSocket = theContext.CreateResponseSocket())
            {
                NetMQMonitor monitor;
                using (var reqSocket = theContext.CreateRequestSocket())
                {
                    monitor = new NetMQMonitor(theContext, reqSocket, "inproc://#monitor", SocketEvent.All);
                    Task.Factory.StartNew(() => monitor.Start());

                    //The bug is only occurring when monitor a tcp socket
                    var port = resSocket.BindRandomPort("tcp://127.0.0.1");
                    reqSocket.Connect("tcp://127.0.0.1:" + port);

                    reqSocket.Send("question");
                    Assert.That(resSocket.ReceiveFrameString(), Is.EqualTo("question"));
                    resSocket.Send("response");
                    Assert.That(reqSocket.ReceiveFrameString(), Is.EqualTo("response"));
                }
                Thread.Sleep(100);
                // Monitor.Dispose should complete
                var completed = Task.Factory.StartNew(() => monitor.Dispose()).Wait(1000);
                Assert.That(completed, Is.True);
            }
            //Note: If this test fails, it will hang because the context Dispose will block
        }
    }
}
