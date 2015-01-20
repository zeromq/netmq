using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NetMQ.zmq;

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

                        rep.Bind("tcp://127.0.0.1:5002");

                        using (var req = contex.CreateRequestSocket())
                        {
                            req.Connect("tcp://127.0.0.1:5002");
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

                            rep.Bind("tcp://127.0.0.1:5002");


                            req.Connect("tcp://127.0.0.1:5002");
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
                    resSocket.Bind("tcp://127.0.0.1:12345");
                    reqSocket.Connect("tcp://127.0.0.1:12345");

                    reqSocket.Send("question");
                    Assert.That(resSocket.ReceiveString(), Is.EqualTo("question"));
                    resSocket.Send("response");
                    Assert.That(reqSocket.ReceiveString(), Is.EqualTo("response"));
                }
                Thread.Sleep(100);
                // Monitor.Dispose should complete
                var completed = Task.Factory.StartNew(() => monitor.Dispose()).Wait(1000);
                Assert.That(completed, Is.True);
                // Task should be faulted with a EndpointNotFoundException
                Assert.That(monitorTask.IsFaulted, Is.True);
                Assert.That(monitorTask.Exception.InnerException, Is.TypeOf<EndpointNotFoundException>());
            }
            //Note: If this test fails, it will hang because the context Dispose will block
        }
    }
}
