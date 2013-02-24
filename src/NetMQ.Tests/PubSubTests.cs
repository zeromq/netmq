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

		[Test, ExpectedException(typeof(AgainException))]
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

		/// <summary>
		/// This test trying to reproduce bug #45 NetMQ.zmq.Utils.Realloc broken!
		/// </summary>
		[Test]		
		public void MultipleSubscriptions()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");
						sub.Subscribe("C");
						sub.Subscribe("B");
						sub.Subscribe("A");						
						sub.Subscribe("D");
						sub.Subscribe("E");

						Thread.Sleep(500);
						
						sub.Unsubscribe("C");
						sub.Unsubscribe("B");
						sub.Unsubscribe("A");
						sub.Unsubscribe("D");
						sub.Unsubscribe("E");

						Thread.Sleep(500);
					}
				}
			}
		}

		[Test]
		public void MultipleSubscribers()
		{
			using (Context contex = Context.Create())
			{
				using (PublisherSocket pub = contex.CreatePublisherSocket())
				{
					pub.Bind("tcp://127.0.0.1:5002");

					using (SubscriberSocket sub = contex.CreateSubscriberSocket())
					using (SubscriberSocket sub2 = contex.CreateSubscriberSocket())
					{
						sub.Connect("tcp://127.0.0.1:5002");
						sub.Subscribe("A");
						sub.Subscribe("AB");
						sub.Subscribe("B");
						sub.Subscribe("C");

						sub2.Connect("tcp://127.0.0.1:5002");
						sub2.Subscribe("A");
						sub2.Subscribe("AB");
						sub2.Subscribe("C");

						Thread.Sleep(500);

						pub.SendTopic("AB").Send("1");

						IList<string> message = sub.ReceiveAllString();

						Assert.AreEqual("AB", message[0]);

						message = sub2.ReceiveAllString();

						Assert.AreEqual("AB", message[0]);
					}
				}
			}
		}

		[Test, ExpectedException(typeof(AgainException))]
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
