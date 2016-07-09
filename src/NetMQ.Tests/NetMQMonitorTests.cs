using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture(Category = "Monitor")]
    public class NetMQMonitorTests
    {
        [Test]
        public void Monitoring()
        {            
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            using (var monitor = new NetMQMonitor(rep, $"inproc://rep.inproc", SocketEvents.Accepted | SocketEvents.Listening))
            {
                var listening = false;
                var accepted = false;

                monitor.Accepted += (s, a) => { accepted = true; };
                monitor.Listening += (s, a) => { listening = true; };

                monitor.Timeout = TimeSpan.FromMilliseconds(100);

                var monitorTask = Task.Factory.StartNew(monitor.Start);

                Thread.Sleep(10);

                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);

                req.SendFrame("a");
                rep.SkipFrame();

                rep.SendFrame("b");
                req.SkipFrame();

                Thread.Sleep(200);

                Assert.IsTrue(listening);
                Assert.IsTrue(accepted);

                monitor.Stop();

                Thread.Sleep(200);

                Assert.IsTrue(monitorTask.IsCompleted);                
            }            
        }

#if !NET35
        [Test]
        public void StartAsync()
        {          
            using (var rep = new ResponseSocket())
            using (var monitor = new NetMQMonitor(rep, "inproc://foo", SocketEvents.Closed))
            {
                var task = monitor.StartAsync();
                Thread.Sleep(200);
                Assert.AreEqual(TaskStatus.Running, task.Status);
                monitor.Stop();
                Assert.True(task.Wait(TimeSpan.FromMilliseconds(1000)));
            }
        }
#endif

        [Test]
        public void NoHangWhenMonitoringUnboundInprocAddress()
        {                        
            using (var monitor = new NetMQMonitor(new PairSocket(), "inproc://unbound-inproc-address", ownsSocket: true))
            {
                var task = Task.Factory.StartNew(monitor.Start);
                monitor.Stop();

                try
                {
                    task.Wait(TimeSpan.FromMilliseconds(1000));
                    Assert.Fail("Exception expected");
                }
                catch (AggregateException ex)
                {
                    Assert.AreEqual(1, ex.InnerExceptions.Count);
                    Assert.IsTrue(ex.InnerExceptions.Single() is EndpointNotFoundException);
                }
            }
        }

        [Test]
        public void ErrorCodeTest()
        {            
            using (var req = new RequestSocket())
            using (var rep = new ResponseSocket())
            using (var monitor = new NetMQMonitor(req, "inproc://rep.inproc", SocketEvents.ConnectDelayed))
            {
                var eventArrived = false;

                monitor.ConnectDelayed += (s, a) => { eventArrived = true; };

                monitor.Timeout = TimeSpan.FromMilliseconds(100);

                var monitorTask = Task.Factory.StartNew(monitor.Start);
                Thread.Sleep(10);

                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);

                req.SendFrame("a");
                rep.SkipFrame();

                rep.SendFrame("b");
                req.SkipFrame();

                Thread.Sleep(200);

                Assert.IsTrue(eventArrived);

                monitor.Stop();

                Thread.Sleep(200);

                Assert.IsTrue(monitorTask.IsCompleted);
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
            
            using (var res = new ResponseSocket())
            {
                NetMQMonitor monitor;
                using (var req = new RequestSocket())
                {
                    monitor = new NetMQMonitor(req, "inproc://#monitor", SocketEvents.All);
                    Task.Factory.StartNew(monitor.Start);

                    // Bug only occurs when monitoring a tcp socket
                    var port = res.BindRandomPort("tcp://127.0.0.1");
                    req.Connect("tcp://127.0.0.1:" + port);

                    req.SendFrame("question");
                    Assert.That(res.ReceiveFrameString(), Is.EqualTo("question"));
                    res.SendFrame("response");
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
