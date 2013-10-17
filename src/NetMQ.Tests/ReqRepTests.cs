using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class ReqRepTests
	{
		[Test]
		[TestCase("tcp://localhost:5001")]
		[TestCase("tcp://127.0.0.1:5001")]
		[TestCase("tcp://unknownhostname:5001", ExpectedException = typeof(System.Net.Sockets.SocketException))]
		public void SimpleReqRep(string address)
		{
			using (NetMQContext ctx = NetMQContext.Create())
			{
				using (var rep = ctx.CreateResponseSocket())
				{
					rep.Bind(address);

					using (var req = ctx.CreateRequestSocket())
					{
						req.Connect(address);

						req.Send("Hi");

						bool more;

						string requestString = rep.ReceiveString(out more);

						Assert.AreEqual("Hi", requestString);
						Assert.IsFalse(more);

						rep.Send("Hi2");

						string responseString = req.ReceiveString(out more);

						Assert.AreEqual("Hi2", responseString);
						Assert.IsFalse(more);
					}
				}
			}
		}

		[Test]
		public void SendingTwoRequestsInaRow()
		{
			using (NetMQContext ctx = NetMQContext.Create())
			{
				using (var rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://localhost:5002");

					using (var req = ctx.CreateRequestSocket())
					{						
						req.Connect("tcp://localhost:5002");

						req.Send("Hi");

						bool more;
						rep.Receive(out more);

						var ex = Assert.Throws<NetMQException>(() => req.Send("Hi2"));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);											
					}
				}
			}
		}

		[Test]
		public void ReceiveBeforeSending()
		{
			using (NetMQContext ctx = NetMQContext.Create())
			{
				using (var rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://localhost:5001");


					using (var req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://localhost:5001");

						bool more;

						var ex = Assert.Throws<NetMQException>(() => req.ReceiveString(out more));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);
						
					}
				}
			}
		}

		[Test]
		public void SendMessageInResponeBeforeReceiving()
		{
			using (NetMQContext ctx = NetMQContext.Create())
			{
				using (var rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://localhost:5001");

					using (var req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://localhost:5001");

						var ex = Assert.Throws<NetMQException>(() => rep.Send("1"));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);											
					}
				}
			}
		}

		[Test]
		public void SendMultiplartMessage()
		{
			using (NetMQContext ctx = NetMQContext.Create())
			{
				using (var rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://localhost:5001");

					using (var req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://localhost:5001");

						req.SendMore("Hello").Send("World");

						bool more;

						string m1 = rep.ReceiveString(out more);

						Assert.IsTrue(more);
						Assert.AreEqual("Hello", m1);						

						string m2 = rep.ReceiveString(out more);

						Assert.IsFalse(more);
						Assert.AreEqual("World", m2);

						rep.SendMore("Hello").Send("Back");

						string m3 = req.ReceiveString(out more);

						Assert.IsTrue(more);
						Assert.AreEqual("Hello", m3);

						string m4 = req.ReceiveString(out more);

						Assert.IsFalse(more);
						Assert.AreEqual("Back", m4);						
					}
				}
			}
		}
	}
}
