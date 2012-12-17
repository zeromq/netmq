using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

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

		[Test, ExpectedException(typeof(InvalidOperationException))]
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

						req.Send("Hi2");
					}
				}
			}
		}

		[Test, ExpectedException(typeof(InvalidOperationException))]
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

						var text = req.ReceiveString(out more);
					}
				}
			}
		}

		[Test, ExpectedException(typeof(InvalidOperationException))]
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

						rep.Send("1");
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
