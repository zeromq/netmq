using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NetMQ.zmq;

namespace NetMQ.Tests
{
	[TestFixture]
	public class ReqRepTests
	{
		[Test]
		public void SimpleReqRep()
		{
			using (Context ctx = Context.Create())
			{
				using (ResponseSocket rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5001");

					using (RequestSocket req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://127.0.0.1:5001");

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
			using (Context ctx = Context.Create())
			{
				using (ResponseSocket rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5002");

					using (RequestSocket req = ctx.CreateRequestSocket())
					{						
						req.Connect("tcp://127.0.0.1:5002");

						req.Send("Hi");

						bool more;
						rep.Receive(out more);

						var ex = Assert.Throws<zmq.ZMQException>(() => req.Send("Hi2"));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);											
					}
				}
			}
		}

		[Test]
		public void ReceiveBeforeSending()
		{
			using (Context ctx = Context.Create())
			{
				using (ResponseSocket rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5001");
					
					
					using (RequestSocket req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://127.0.0.1:5001");

						bool more;

						var ex = Assert.Throws<zmq.ZMQException>(() => req.ReceiveString(out more));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);
						
					}
				}
			}
		}

		[Test]
		public void SendMessageInResponeBeforeReceiving()
		{
			using (Context ctx = Context.Create())
			{
				using (ResponseSocket rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5001");
					
					using (RequestSocket req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://127.0.0.1:5001");

						var ex = Assert.Throws<zmq.ZMQException>(() => rep.Send("1"));

						Assert.AreEqual(ErrorCode.EFSM, ex.ErrorCode);											
					}
				}
			}
		}

		[Test]
		public void SendMultiplartMessage()
		{
			using (Context ctx = Context.Create())
			{
				using (ResponseSocket rep = ctx.CreateResponseSocket())
				{
					rep.Bind("tcp://127.0.0.1:5001");					

					using (RequestSocket req = ctx.CreateRequestSocket())
					{
						req.Connect("tcp://127.0.0.1:5001");

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
