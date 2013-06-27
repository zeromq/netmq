using NUnit.Framework;
using NetMQ.zmq;

namespace NetMQ.Tests.zmq
{
	[TestFixture]
	public class YQueueTests
	{
		[Test]
		public void PushingToQueueShouldIncreaseBackPosition()
		{
			var queue = new YQueue<string>(100);
			Assert.AreEqual(0, queue.BackPos, "Initial back position should be 0");
			queue.Push("One");
			Assert.AreEqual(1, queue.BackPos, "Back position should be incremented after push");
			queue.Push("Two");
			Assert.AreEqual(2, queue.BackPos, "Back position should be incremented after push");
			queue.Push("Three");
			Assert.AreEqual(3, queue.BackPos, "Back position should be incremented after push");
		}

		[Test]
		public void PoppingFromQueueShouldIncreaseFrontPosition()
		{
			var queue = new YQueue<string>(100);
			queue.Push("One");
			queue.Push("Two");
			queue.Push("Three");
			Assert.AreEqual(0, queue.FrontPos, "Initial front position should be 0");
			queue.Pop();
			Assert.AreEqual(1, queue.FrontPos, "Front position should be incremented after pop");
			queue.Pop();
			Assert.AreEqual(2, queue.FrontPos, "Front position should be incremented after pop");
			queue.Pop();
			Assert.AreEqual(3, queue.FrontPos, "Front position should be incremented after pop");
		}

		[Test]
		public void QueuedItemsShouldBeReturned()
		{
			var queue = new YQueue<string>(100);
			queue.Push("One");
			queue.Push("Two");
			queue.Push("Three");
			Assert.AreEqual("One", queue.Pop(), "First element pushed should be the first popped");
			Assert.AreEqual("Two", queue.Pop(), "Second element pushed should be the second popped");
			Assert.AreEqual("Three", queue.Pop(), "Third element pushed should be the third popped");
		}

		[Test]
		public void SmallChunkSizeShouldNotAffectBehavior()
		{
			var queue = new YQueue<string>(2);
			queue.Push("One");
			queue.Push("Two");
			queue.Push("Three");
			queue.Push("Four");
			queue.Push("Five");
			Assert.AreEqual("One", queue.Pop());
			Assert.AreEqual("Two", queue.Pop());
			Assert.AreEqual("Three", queue.Pop());
			Assert.AreEqual("Four", queue.Pop());
			Assert.AreEqual("Five", queue.Pop());
			Assert.AreEqual(queue.FrontPos, queue.BackPos, "On empty queue the front position should be equal to back position");
		}

		[Test]
		public void UnpushShouldRemoveLastPushedItem()
		{
			var queue = new YQueue<string>(2);
			queue.Push("One");
			queue.Push("Two");
			queue.Push("Three");
			Assert.AreEqual(3, queue.BackPos, "Ensuring that Back position is 3");
			Assert.AreEqual("Three", queue.Unpush(), "Unpush should return the last item in a queue");
			Assert.AreEqual(2, queue.BackPos, "Back position should be decremented after unpush");
			Assert.AreEqual("Two", queue.Unpush(), "Unpush should return the last item in a queue");
			Assert.AreEqual(1, queue.BackPos, "Back position should be decremented after unpush");
			Assert.AreEqual("One", queue.Unpush(), "Unpush should return the last item in a queue");
			Assert.AreEqual(0, queue.BackPos, "Back position should be decremented after unpush");
		}
	}
}
