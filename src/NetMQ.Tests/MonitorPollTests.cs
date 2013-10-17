// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Threading;
#if !PRE_4
using System.Threading.Tasks;
#endif
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
								Console.WriteLine(a.Socket.RemoteEndPoint.ToString());								
							};
						monitor.Listening += (s, a) =>
							{
								listening = true;
								Console.WriteLine(a.Socket.LocalEndPoint.ToString());								
							};

						monitor.Timeout = TimeSpan.FromMilliseconds(100);

#if !PRE_4
						var pollerTask = Task.Factory.StartNew(monitor.Start);
#else
                        var pollerThread = new Thread(_ => monitor.Start());
                        pollerThread.Start();
#endif

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

#if !PRE_4
                            Assert.IsTrue(pollerTask.IsCompleted);
#else
                            Assert.IsFalse(pollerThread.IsAlive);
#endif
                        }
					}
				}
			}
		}
	}
}
