// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
#if !PRE_4
using System.Threading.Tasks;
#else
using System.Threading;
#endif
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class ProxyTests
	{
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
#if !PRE_4
				Task.Factory.StartNew(proxy.Start);
#else
                ThreadPool.QueueUserWorkItem(_ => { proxy.Start(); });
#endif

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
