using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
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
                var listening = false;
                var accepted = false;

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
            using (var context = NetMQContext.Create())
            using (var req = context.CreateRequestSocket())
            using (var rep = context.CreateResponseSocket())
            using (var monitor = new NetMQMonitor(context, req, "inproc://rep.inproc", SocketEvent.ConnectDelayed))
            {
                var eventArrived = false;

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

            using (var context = NetMQContext.Create())
            using (var res = context.CreateResponseSocket())
            {
                NetMQMonitor monitor;
                using (var req = context.CreateRequestSocket())
                {
                    monitor = new NetMQMonitor(context, req, "inproc://#monitor", SocketEvent.All);
                    Task.Factory.StartNew(() => monitor.Start());

                    // Bug only occurs when monitoring a tcp socket
                    var port = res.BindRandomPort("tcp://127.0.0.1");
                    req.Connect("tcp://127.0.0.1:" + port);

                    req.Send("question");
                    Assert.That(res.ReceiveFrameString(), Is.EqualTo("question"));
                    res.Send("response");
                    Assert.That(req.ReceiveFrameString(), Is.EqualTo("response"));
                }
                Thread.Sleep(100);
                // Monitor.Dispose should complete
                var completed = Task.Factory.StartNew(() => monitor.Dispose()).Wait(1000);
                Assert.That(completed, Is.True);
            }
            // NOTE If this test fails, it will hang because context.Dispose will block
        }
    }
}
