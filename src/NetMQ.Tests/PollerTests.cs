using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class PollerTests
	{
		[Test]
		public void ResponsePoll()
		{
			using (Context contex = Context.Create())
			{
				using (ResponseSocket rep = contex.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5002");

					using (RequestSocket req = contex.CreateRequestSocket())
					{
						req.Connect("tcp://127.0.0.1:5002");

						Poller poller = new Poller(contex);

						poller.AddSocket(rep, r =>
																		{
																			bool more;
																			string m = r.ReceiveString(out more);

																			Assert.False(more);
																			Assert.AreEqual("Hello", m);

																			r.Send("World");
																		});

						Task pollerTask = Task.Factory.StartNew(poller.Start);

						req.Send("Hello");

						bool more2;
						string m1 = req.ReceiveString(out more2);

						Assert.IsFalse(more2);
						Assert.AreEqual("World", m1);

						poller.Stop();

						Thread.Sleep(100);
						Assert.IsTrue(pollerTask.IsCompleted);
					}
				}
			}
		}

		[Test]
		public void MonitoringPoll()
		{
			bool listening = false;
			bool connected = false;
			bool accepted = false;

			using (Context contex = Context.Create())
			{
				Poller poller = new Poller(contex);

				using (ResponseSocket rep = contex.CreateResponseSocket())
				{
					MonitoringEventsHandler repMonitor = new MonitoringEventsHandler();
					repMonitor.OnAccepted = (addr, fd) => accepted = true;
					repMonitor.OnListening = (addr, fd) => listening = true;

					poller.AddMonitor(rep, "inproc://rep.inproc", repMonitor, true);
					rep.Bind("tcp://127.0.0.1:5002");

					using (RequestSocket req = contex.CreateRequestSocket())
					{
						MonitoringEventsHandler reqMonitor = new MonitoringEventsHandler();
						reqMonitor.OnConnected = (addr, fd) => connected = true;

						poller.AddMonitor(req, "inproc://req.inproc", reqMonitor, true);

						var pollerTask = Task.Factory.StartNew(poller.Start);

						req.Connect("tcp://127.0.0.1:5002");
						req.Send("a");

						bool more;

						string m = rep.ReceiveString(out more);

						rep.Send("b");

						string m2 = req.ReceiveString(out more);

						Assert.IsTrue(listening);
						Assert.IsTrue(connected);
						Assert.IsTrue(accepted);

						poller.Stop();
						Thread.Sleep(100);

						Assert.IsTrue(pollerTask.IsCompleted);
					}
				}
			}
		}

		[Test]
		public void ProxyPoll()
		{
			using (Context contex = Context.Create())
			{
				Poller poller = new Poller(contex);

				RouterSocket router = contex.CreateRouterSocket();
				router.Bind("tcp://127.0.0.1:5001");

				RequestSocket req = contex.CreateRequestSocket();
				req.Connect("tcp://127.0.0.1:5001");

				ResponseSocket rep = contex.CreateResponseSocket();
				rep.Bind("tcp://127.0.0.1:5002");

				DealerSocket dealer = contex.CreateDealerSocket();
				dealer.Connect("tcp://127.0.0.1:5002");

				poller.AddProxy(router, dealer, true);

				Task pollerTask = Task.Factory.StartNew(poller.Start);

				req.Send("a");

				bool more;

				string m = rep.ReceiveString(out more);

				Assert.AreEqual("a", m);
				Assert.IsFalse(more);

				rep.Send("b");

				string m2 = req.ReceiveString(out more);

				Assert.AreEqual("b", m2);
				Assert.IsFalse(more);

				poller.Stop();

				Thread.Sleep(100);

				Assert.IsTrue(pollerTask.IsCompleted);

				router.Dispose();
				dealer.Dispose();
				req.Dispose();
				rep.Dispose();
			}
		}
	}
}
