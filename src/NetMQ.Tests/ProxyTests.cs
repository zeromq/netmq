using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class ProxyTests
	{
		[Test]
		public void TestInfinitePolling()
		{
			using (var context = NetMQContext.Create())
			using (var socket = context.CreateDealerSocket())
			{
				socket.Bind("inproc://test");
				//Workaround. To signal the poll that we are interested in POLLIN events, otherwise the Poll will be waiting for errors only.
				socket.ReceiveReady += (sender, args) => { };

				// ReSharper disable AccessToDisposedClosure
				Task.Factory.StartNew(() =>
					{
						Thread.Sleep(500);
						using (var monitor = context.CreateDealerSocket())
						{
							monitor.Connect("inproc://test");
							monitor.Send("ping");
						}
					});

				Assert.DoesNotThrow(socket.Poll);
				Assert.AreEqual(socket.ReceiveString(SendReceiveOptions.DontWait), "ping");
				// ReSharper restore AccessToDisposedClosure
			}
		}

		[Test]
		public void TestProxySendAndReceive()
		{
			using (var ctx = NetMQContext.Create())
			using (var front = ctx.CreateRouterSocket())
			using (var back = ctx.CreateDealerSocket())
			{
				front.Bind("inproc://frontend");
				back.Bind("inproc://backend");

				var proxy = new Proxy(front, back, null);
				Task.Factory.StartNew(proxy.Start);

				using (var client = ctx.CreateRequestSocket())
				using (var server = ctx.CreateResponseSocket())
				{
					client.Connect("inproc://frontend");
					server.Connect("inproc://backend");

					client.Send("hello");
					Assert.AreEqual("hello", server.ReceiveString());
					server.Send("reply");
					Assert.AreEqual("reply", client.ReceiveString());
				}
			}
		}
	}
}
