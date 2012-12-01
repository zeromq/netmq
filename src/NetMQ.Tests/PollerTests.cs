using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
			System.Security.Cryptography.RSACryptoServiceProvider s = new RSACryptoServiceProvider(new CspParameters());


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

		[Test]
		public void AddSocketDuringWork()
		{
			using (Context contex = Context.Create())
			{
				// we are using three responses to make sure we actually move the correct socket and other sockets still work
				using (RouterSocket router = contex.CreateRouterSocket())
				using (RouterSocket router2 = contex.CreateRouterSocket())
				{
					router.Bind("tcp://127.0.0.1:5002");
					router2.Bind("tcp://127.0.0.1:5003");

					using (DealerSocket dealer = contex.CreateDealerSocket())
					using (DealerSocket dealer2 = contex.CreateDealerSocket())
					{
						dealer.Connect("tcp://127.0.0.1:5002");
						dealer2.Connect("tcp://127.0.0.1:5003");

						bool router1arrived = false;
						bool router2arrived = false;

						Poller poller = new Poller(contex);

						bool more;

						poller.AddSocket(router, (r) =>
																			 {
																				 router1arrived = true;

																				 router.Receive(out more);
																				 router.Receive(out more);
																				 poller.AddSocket(router2, (r2) =>
																																		 {
																																			 router2.Receive(out more);
																																			 router2.Receive(out more);
																																			 router2arrived = true;
																																		 });
																			 });

						Task task = Task.Factory.StartNew(poller.Start);

						dealer.Send("1");
						Thread.Sleep(100);
						dealer2.Send("2");
						Thread.Sleep(100);

						poller.Stop(true);
						task.Wait();

						Assert.IsTrue(router1arrived);
						Assert.IsTrue(router2arrived);
					}
				}
			}
		}

		[Test]
		public void AddSocketAfterRemoving()
		{
			using (Context contex = Context.Create())
			{
				// we are using three responses to make sure we actually move the correct socket and other sockets still work
				using (RouterSocket router = contex.CreateRouterSocket())
				using (RouterSocket router2 = contex.CreateRouterSocket())
				using (RouterSocket router3 = contex.CreateRouterSocket())
				{
					router.Bind("tcp://127.0.0.1:5002");
					router2.Bind("tcp://127.0.0.1:5003");
					router3.Bind("tcp://127.0.0.1:5004");

					using (DealerSocket dealer = contex.CreateDealerSocket())
					using (DealerSocket dealer2 = contex.CreateDealerSocket())
					using (DealerSocket dealer3 = contex.CreateDealerSocket())
					{
						dealer.Connect("tcp://127.0.0.1:5002");
						dealer2.Connect("tcp://127.0.0.1:5003");
						dealer3.Connect("tcp://127.0.0.1:5004");

						bool router1arrived = false;
						bool router2arrived = false;
						bool router3arrived = false;


						Poller poller = new Poller(contex);

						bool more;

						poller.AddSocket(router, (r) =>
						{
							router1arrived = true;

							router.Receive(out more);
							router.Receive(out more);

							poller.CancelSocket(router);

						});

						poller.AddSocket(router2, (r) =>
																				{
																					router2arrived = true;
																					router2.Receive(out more);
																					router2.Receive(out more);

																					poller.AddSocket(router3, (r2) =>
						{
							router3.Receive(out more);
							router3.Receive(out more);
							router3arrived = true;
						});
																				});

						Task task = Task.Factory.StartNew(poller.Start);

						dealer.Send("1");
						Thread.Sleep(100);
						dealer2.Send("2");
						Thread.Sleep(100);
						dealer3.Send("3");
						Thread.Sleep(100);

						poller.Stop(true);
						task.Wait();

						Assert.IsTrue(router1arrived);
						Assert.IsTrue(router2arrived);
						Assert.IsTrue(router3arrived);
					}
				}
			}
		}

		[Test]
		public void AddTwoSocketAfterRemoving()
		{
			using (Context contex = Context.Create())
			{
				// we are using three responses to make sure we actually move the correct socket and other sockets still work
				using (RouterSocket router = contex.CreateRouterSocket())
				using (RouterSocket router2 = contex.CreateRouterSocket())
				using (RouterSocket router3 = contex.CreateRouterSocket())
				using (RouterSocket router4 = contex.CreateRouterSocket())
				{
					router.Bind("tcp://127.0.0.1:5002");
					router2.Bind("tcp://127.0.0.1:5003");
					router3.Bind("tcp://127.0.0.1:5004");
					router4.Bind("tcp://127.0.0.1:5005");

					using (DealerSocket dealer = contex.CreateDealerSocket())
					using (DealerSocket dealer2 = contex.CreateDealerSocket())
					using (DealerSocket dealer3 = contex.CreateDealerSocket())
					using (DealerSocket dealer4 = contex.CreateDealerSocket())
					{
						dealer.Connect("tcp://127.0.0.1:5002");
						dealer2.Connect("tcp://127.0.0.1:5003");
						dealer3.Connect("tcp://127.0.0.1:5004");
						dealer4.Connect("tcp://127.0.0.1:5005");


						int router1arrived = 0;
						int router2arrived = 0;
						bool router3arrived = false;
						bool router4arrived = false;

						Poller poller = new Poller(contex);

						bool more;

						poller.AddSocket(router, (r) =>
																			 {
																				 router1arrived++;

																				 router.Receive(out more);
																				 router.Receive(out more);

																				 poller.CancelSocket(router);

																			 });

						poller.AddSocket(router2, (r) =>
						{
							router2arrived++;
							router2.Receive(out more);
							router2.Receive(out more);

							if (router2arrived == 1)
							{
								poller.AddSocket(router3, (r2) =>
									                          {
										                          router3.Receive(out more);
										                          router3.Receive(out more);
										                          router3arrived = true;
									                          });

								poller.AddSocket(router4, (r3) =>
									                          {
										                          router4.Receive(out more);
										                          router4.Receive(out more);
										                          router4arrived = true;
									                          });
							}
						});

						Task task = Task.Factory.StartNew(poller.Start);

						dealer.Send("1");
						Thread.Sleep(100);
						dealer2.Send("2");
						Thread.Sleep(100);
						dealer3.Send("3");
						dealer4.Send("4");
						dealer2.Send("2");
						dealer.Send("1");
						Thread.Sleep(100);

						poller.Stop(true);
						task.Wait();

						router.Receive(true, out more);

						Assert.IsTrue(more);

						router.Receive(true, out more);

						Assert.IsFalse(more);

						Assert.AreEqual(1,router1arrived);
						Assert.AreEqual(2,router2arrived);
						Assert.IsTrue(router3arrived);
						Assert.IsTrue(router4arrived);
					}
				}
			}
		}


		[Test]
		public void CancelSocket()
		{
			using (Context contex = Context.Create())
			{
				// we are using three responses to make sure we actually move the correct socket and other sockets still work
				using (RouterSocket router = contex.CreateRouterSocket())
				using (RouterSocket router2 = contex.CreateRouterSocket())
				using (RouterSocket router3 = contex.CreateRouterSocket())
				{
					router.Bind("tcp://127.0.0.1:5002");
					router2.Bind("tcp://127.0.0.1:5003");
					router3.Bind("tcp://127.0.0.1:5004");

					using (DealerSocket dealer = contex.CreateDealerSocket())
					using (DealerSocket dealer2 = contex.CreateDealerSocket())
					using (DealerSocket dealer3 = contex.CreateDealerSocket())
					{
						dealer.Connect("tcp://127.0.0.1:5002");
						dealer2.Connect("tcp://127.0.0.1:5003");
						dealer3.Connect("tcp://127.0.0.1:5004");

						Poller poller = new Poller(contex);

						bool first = true;

						poller.AddSocket(router2, s =>
						{
							bool more;

							// identity
							byte[] identity = s.Receive(out more);

							// message
							s.Receive(out more);

							s.SendMore(identity);
							s.Send("2");
						});

						poller.AddSocket(router, r =>
						{
							if (!first)
							{
								Assert.Fail("This should happen because we cancelled the socket");
							}
							first = false;

							bool more;

							// identity
							r.Receive(out more);

							string m = r.ReceiveString(out more);

							Assert.False(more);
							Assert.AreEqual("Hello", m);

							// cancellign the socket
							poller.CancelSocket(r);
						});

						poller.AddSocket(router3, s =>
						{
							bool more;

							// identity
							byte[] identity = s.Receive(out more);

							// message
							s.Receive(out more);

							s.SendMore(identity); s.Send("3");
						});

						Task pollerTask = Task.Factory.StartNew(poller.Start);

						dealer.Send("Hello");

						// sending this should not arrive on the poller, therefore response for this will never arrive
						dealer.Send("Hello2");

						Thread.Sleep(100);

						// sending this should not arrive on the poller, therefore response for this will never arrive						
						dealer.Send("Hello3");

						Thread.Sleep(500);

						bool more2;

						// making sure the socket defined before the one cancelled still works
						dealer2.Send("1");
						string msg = dealer2.ReceiveString(out more2);
						Assert.AreEqual("2", msg);

						// making sure the socket defined after the one cancelled still works
						dealer3.Send("1");
						msg = dealer3.ReceiveString(out more2);
						Assert.AreEqual("3", msg);

						// we have to give this some time if we want to make sure it's really not happening and it not only because of time
						Thread.Sleep(300);

						poller.Stop();

						Thread.Sleep(100);
						Assert.IsTrue(pollerTask.IsCompleted);
					}
				}
			}
		}
	}
}
