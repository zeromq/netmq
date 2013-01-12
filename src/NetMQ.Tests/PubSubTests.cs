using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace NetMQ.Tests
{
	[TestFixture]
	public class PubSubTests
	{
		[Test]
		public void TopicPubSub()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");
						sub.Subscribe("A");

						// let the subscrbier connect to the publisher before sending a message
						Thread.Sleep(500);

						pub.SendTopic("A").Send("Hello");

						bool more;

						string m = sub.ReceiveString(out more);

						Assert.AreEqual("A", m);
						Assert.IsTrue(more);

						string m2 = sub.ReceiveString(out more);

						Assert.AreEqual("Hello", m2);
						Assert.False(more);
					}
				}
			}
		}

		[Test]
		public void SimplePubSub()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");						
						sub.Subscribe("");

						// let the subscrbier connect to the publisher before sending a message
						Thread.Sleep(500);

						pub.Send("Hello");

						bool more;
						
						string m = sub.ReceiveString(out more);

						Assert.AreEqual("Hello", m);
						Assert.False(more);
					}
				}
			}
		}

		[Test, ExpectedException(typeof(zmq.ZMQException))]
		public void NotSubscribed()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");						

						// let the subscrbier connect to the publisher before sending a message
						Thread.Sleep(500);

						pub.Send("Hello");

						bool more;

						string m = sub.ReceiveString(true, out more);						
					}
				}
			}
		}

		[Test, ExpectedException(typeof(zmq.ZMQException))]
		public void UnSubscribe()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");
						sub.Subscribe("A");

						// let the subscrbier connect to the publisher before sending a message
						Thread.Sleep(500);

						pub.SendTopic("A").Send("Hello");

						bool more;

						string m = sub.ReceiveString(out more);

						Assert.AreEqual("A", m);
						Assert.IsTrue(more);

						string m2 = sub.ReceiveString(out more);

						Assert.AreEqual("Hello", m2);
						Assert.False(more);

						sub.Unsubscribe("A");

						Thread.Sleep(500);

						pub.SendTopic("A").Send("Hello");

						string m3  = sub.ReceiveString(true, out more);
					}
				}
			}
		}
	}
}
