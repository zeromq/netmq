using System;
using System.Collections.Generic;
using System.Linq;
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
								Console.WriteLine(a.Socket.RemoteEndPoint.ToString());								
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
	}
}
