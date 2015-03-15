using NetMQ.Core.Utils;
using NUnit.Framework;

namespace NetMQ.Tests.Core
{
    [TestFixture]
    public class YQueueTests
    {
        [Test]
        public void PushingToQueueShouldIncreaseBackPosition()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(100);
            Assert.AreEqual(0, queue.BackPos, "Initial back position should be 0");
            queue.Push(ref one);
            Assert.AreEqual(1, queue.BackPos, "Back position should be incremented after push");
            queue.Push(ref two);
            Assert.AreEqual(2, queue.BackPos, "Back position should be incremented after push");
            queue.Push(ref three);
            Assert.AreEqual(3, queue.BackPos, "Back position should be incremented after push");
        }

        [Test]
        public void PoppingFromQueueShouldIncreaseFrontPosition()
        {
            var queue = new YQueue<string>(100);

            string one = "One";
            string two = "Two";
            string three = "Three";

            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
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
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(100);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            Assert.AreEqual("One", queue.Pop(), "First element pushed should be the first popped");
            Assert.AreEqual("Two", queue.Pop(), "Second element pushed should be the second popped");
            Assert.AreEqual("Three", queue.Pop(), "Third element pushed should be the third popped");
        }

        [Test]
        public void SmallChunkSizeShouldNotAffectBehavior()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";
            string four = "Four";
            string five = "Five";


            var queue = new YQueue<string>(2);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            queue.Push(ref four);
            queue.Push(ref five);
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
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(2);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
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
