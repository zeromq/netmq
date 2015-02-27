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
            bool listening = false;
            bool accepted = false;

            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var rep = contex.CreateResponseSocket())
                {
                    using (NetMQMonitor monitor = new NetMQMonitor(contex, rep, "inproc://rep.inproc", SocketEvent.Accepted | SocketEvent.Listening))
                    {
                        monitor.Accepted += (s, a) =>
                            {
                                accepted = true;
                                //Console.WriteLine(a.Socket.LocalEndPoint.ToString());
                            };
                        monitor.Listening += (s, a) =>
                            {
                                listening = true;
                                Console.WriteLine(a.Socket.LocalEndPoint.ToString());
                            };

                        monitor.Timeout = TimeSpan.FromMilliseconds(100);

                        var pollerTask = Task.Factory.StartNew(monitor.Start);

                        var port2 = rep.BindRandomPort("tcp://127.0.0.1");

                        using (var req = contex.CreateRequestSocket())
                        {
                            req.Connect("tcp://127.0.0.1:" + port2);
                            req.Send("a");

                            bool more;

                            string m = rep.ReceiveString(out more);

                            rep.Send("b");

                            string m2 = req.ReceiveString(out more);

                            Thread.Sleep(200);

                            Assert.IsTrue(listening);
                            Assert.IsTrue(accepted);

                            monitor.Stop();

                            Thread.Sleep(200);

                            Assert.IsTrue(pollerTask.IsCompleted);
                        }
                    }
                }
            }
        }

        [Test]
        public void ErrorCodeTest()
        {
            bool eventArrived = false;

            using (NetMQContext contex = NetMQContext.Create())
            {
                using (var req = contex.CreateRequestSocket())
                {
                    using (var rep = contex.CreateResponseSocket())
                    {
                        using (NetMQMonitor monitor =
                            new NetMQMonitor(contex, req, "inproc://rep.inproc", SocketEvent.ConnectDelayed))
                        {
                            monitor.ConnectDelayed += (s, a) =>
                            {
                                eventArrived = true;
                            };

                            monitor.Timeout = TimeSpan.FromMilliseconds(100);

                            var pollerTask = Task.Factory.StartNew(monitor.Start);

                            var port = rep.BindRandomPort("tcp://127.0.0.1");


                            req.Connect("tcp://127.0.0.1:" + port);
                            req.Send("a");

                            bool more;

                            string m = rep.ReceiveString(out more);

                            rep.Send("b");

                            string m2 = req.ReceiveString(out more);

                            Thread.Sleep(200);

                            Assert.IsTrue(eventArrived);

                            monitor.Stop();

                            Thread.Sleep(200);

                            Assert.IsTrue(pollerTask.IsCompleted);
                        }
                    }
                }
            }
        }

        [Test]
        public void MonitorDisposeProperlyWhenDisposedAfterMonitoredTcpSocket()
        {
            // The bug:
            // Given we monitor a netmq tcp socket
            // Given we disposed of the monitored socket first
            // When we dipose of the monitor
            // Then our monitor is Faulted with a EndpointNotFoundException
            // And monitor can't be stopped or disposed

            Task monitorTask;
            using (var theContext = NetMQContext.Create())
            using (var resSocket = theContext.CreateResponseSocket())
            {
                NetMQMonitor monitor = null;
                using (var reqSocket = theContext.CreateRequestSocket())
                {
                    monitor = new NetMQMonitor(theContext, reqSocket, "inproc://#monitor", SocketEvent.All);
                    monitorTask = Task.Factory.StartNew(() => monitor.Start());

                    //The bug is only occuring when monitor a tcp socket
                    var port = resSocket.BindRandomPort("tcp://127.0.0.1");
                    reqSocket.Connect("tcp://127.0.0.1:" + port);

                    reqSocket.Send("question");
                    Assert.That(resSocket.ReceiveString(), Is.EqualTo("question"));
                    resSocket.Send("response");
                    Assert.That(reqSocket.ReceiveString(), Is.EqualTo("response"));
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
