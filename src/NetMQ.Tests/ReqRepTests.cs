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
					rep.Options.Linger = TimeSpan.Zero;

					rep.Bind("tcp://127.0.0.1:5001");

					using (RequestSocket req = ctx.CreateRequestSocket())
					{
						req.Options.Linger = TimeSpan.Zero;

						req.Connect("tcp://127.0.0.1:5001");

						req.Send("Hi");

						req.Send("Hi2");
					}
				}
			}
		}
	}
}
