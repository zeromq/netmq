using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.Sockets;

namespace NetMQ.Tests
{
	[TestFixture]
	public class MonitorPollTests
	{
		[Test]
		public void MonitoringPoll()
		{
			bool listening = false;
			bool accepted = false;

			using (Context contex = Context.Create())
			{
				using (IResponseSocket rep = contex.CreateResponseSocket())
				{
					MonitoringEventsHandler repMonitor = new MonitoringEventsHandler();
					repMonitor.OnAccepted = (addr, fd) => accepted = true;
					repMonitor.OnListening = (addr, fd) => listening = true;

					MonitorPoll monitorPoll = new MonitorPoll(contex, rep, "inproc://rep.inproc", repMonitor);
					monitorPoll.Timeout = TimeSpan.FromMilliseconds(100);

					var pollerTask = Task.Factory.StartNew(monitorPoll.Start);

					rep.Bind("tcp://127.0.0.1:5002");

					using (IRequestSocket req = contex.CreateRequestSocket())
					{												
						req.Connect("tcp://127.0.0.1:5002");
						req.Send("a");

						bool more;

						string m = rep.ReceiveString(out more);

						rep.Send("b");

						string m2 = req.ReceiveString(out more);

						Assert.IsTrue(listening);						
						Assert.IsTrue(accepted);

						monitorPoll.Stop();
						Thread.Sleep(200);

						Assert.IsTrue(pollerTask.IsCompleted);
					}
				}
			}
		}
	}
}
