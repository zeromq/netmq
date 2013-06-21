using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetMQ.zmq;

namespace NetMQ.Tests.zmq
{
	[TestFixture]
	public class ZMQPollTests
	{
		[Test]
		public void ShouldAllowInfinitePolling()
		{
			Ctx contextNew = ZMQ.CtxNew();
			SocketBase receiver = ZMQ.Socket(contextNew, ZmqSocketType.Dealer);

			ZMQ.Bind(receiver, "inproc://test");

			Task.Factory.StartNew(() =>
			{
				Thread.Sleep(500);
				SocketBase sender = ZMQ.Socket(contextNew, ZmqSocketType.Dealer);
				ZMQ.Connect(sender, "inproc://test");
				ZMQ.Send(sender, "ping", SendReceiveOptions.None);
				ZMQ.Close(sender);
			});

			var pollItems = new PollItem[] { new PollItem(receiver, PollEvents.PollIn) };

			Assert.DoesNotThrow(() => ZMQ.Poll(pollItems, -1));

			var actual = Encoding.ASCII.GetString(ZMQ.Recv(receiver, SendReceiveOptions.DontWait).Data);
			Assert.AreEqual("ping", actual);

			ZMQ.Close(receiver);
			ZMQ.Term(contextNew);
		}
	}
}
